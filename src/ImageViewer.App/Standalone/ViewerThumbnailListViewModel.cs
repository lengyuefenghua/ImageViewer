using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Services;
using ImageViewer.Core.Diagnostics;

namespace ImageViewer.App.Standalone
{
    // 缩略图列表项：Bitmap 为 null 且 IsPlaceholder 为真表示解码失败（不再重试）。
    public sealed class ViewerThumbnailItem : INotifyPropertyChanged
    {
        public ViewerThumbnailItem(int index, string path)
        {
            Index = index;
            Path = path;
            FileName = System.IO.Path.GetFileName(path);
        }

        public int Index { get; private set; }
        public string Path { get; private set; }
        public string FileName { get; private set; }
        public object Bitmap { get; private set; }
        public bool IsPlaceholder { get; private set; }
        public bool IsCurrent { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;

        internal void SetBitmap(object bitmap)
        {
            Bitmap = bitmap;
            IsPlaceholder = false;
            Changed("Bitmap");
            Changed("IsPlaceholder");
        }

        internal void SetPlaceholder()
        {
            Bitmap = null;
            IsPlaceholder = true;
            Changed("Bitmap");
            Changed("IsPlaceholder");
        }

        internal void SetCurrent(bool current)
        {
            IsCurrent = current;
            Changed("IsCurrent");
        }

        private void Changed(string name)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }

    // 独立查看器的左侧缩略图列表：纯内存（不写运行缓存/磁盘缓存），LRU 上限，
    // Dispose 时取消在途加载并清空缓存；缩略图解码复用共享只读打开（不占用图片文件）。
    public sealed class ViewerThumbnailListViewModel : IDisposable
    {
        public const int ThumbnailWidth = 120;
        public const int DefaultCacheCapacity = 300;
        private const int MaxConcurrentLoads = 2;
        private const int PrefetchRadius = 20;
        private const string LoggerName = "ImageViewer";

        private readonly object sync = new object();
        private readonly Func<string, Task<object>> loadThumbnail;
        private readonly int cacheCapacity;
        private readonly int maxConcurrentLoads;
        private readonly LinkedList<string> cacheOrder = new LinkedList<string>();
        private readonly Dictionary<string, object> cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<int> pending = new List<int>();
        private readonly HashSet<int> queued = new HashSet<int>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly SynchronizationContext uiContext;
        private int activeLoads;
        private int currentIndex = -1;
        private volatile bool disposed;

        public ViewerThumbnailListViewModel(
            IReadOnlyList<string> paths,
            Func<string, Task<object>> loadThumbnail = null,
            int cacheCapacity = DefaultCacheCapacity,
            int maxConcurrentLoads = MaxConcurrentLoads)
        {
            if (paths == null) throw new ArgumentNullException("paths");

            this.loadThumbnail = loadThumbnail ?? LoadThumbnailFromDisk;
            this.cacheCapacity = Math.Max(1, cacheCapacity);
            this.maxConcurrentLoads = Math.Max(1, maxConcurrentLoads);
            uiContext = SynchronizationContext.Current as System.Windows.Threading.DispatcherSynchronizationContext;
            Items = new ObservableCollection<ViewerThumbnailItem>();
            for (var index = 0; index < paths.Count; index++)
            {
                Items.Add(new ViewerThumbnailItem(index + 1, paths[index]));
            }
            IsVisible = true;
        }

        public ObservableCollection<ViewerThumbnailItem> Items { get; private set; }
        public bool IsVisible { get; private set; }
        internal int CachedItemCount { get { lock (sync) { return cache.Count; } } }
        internal bool IsCached(string path) { lock (sync) { return cache.ContainsKey(path); } }

        public void SetCurrentIndex(int index)
        {
            if (index < 0 || index >= Items.Count) return;

            int previous;
            lock (sync)
            {
                if (disposed || currentIndex == index) return;
                previous = currentIndex;
                currentIndex = index;
            }

            if (previous >= 0 && previous < Items.Count) Items[previous].SetCurrent(false);
            Items[index].SetCurrent(true);
            EnqueueAround(index);
            Pump();
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
        }

        // 测试与诊断用：等待当前排队与在途的缩略图加载全部结束。
        public async Task WaitForPendingLoadsAsync()
        {
            while (true)
            {
                lock (sync)
                {
                    if (pending.Count == 0 && activeLoads == 0) return;
                }
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                pending.Clear();
                queued.Clear();
                cache.Clear();
                cacheOrder.Clear();
                failed.Clear();
            }

            lifetime.Cancel();
            lifetime.Dispose();
            Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "缩略图列表已释放：取消加载并清空内存缓存", null);
        }

        private static Task<object> LoadThumbnailFromDisk(string path)
        {
            // BitmapSourceLoader 以 FileShare.ReadWrite | Delete 打开并降采样解码：不占用文件、不落盘。
            return Task.Run(() => BitmapSourceLoader.Load(path, ThumbnailWidth));
        }

        private void EnqueueAround(int index)
        {
            lock (sync)
            {
                if (disposed) return;
                AddPending(index);
                for (var offset = 1; offset <= PrefetchRadius; offset++)
                {
                    AddPending(index - offset);
                    AddPending(index + offset);
                }
            }
        }

        private void AddPending(int index)
        {
            if (index < 0 || index >= Items.Count) return;
            var item = Items[index];
            if (item.Bitmap != null || failed.Contains(item.Path) || queued.Contains(index)) return;
            pending.Add(index);
            queued.Add(index);
        }

        private void Pump()
        {
            while (true)
            {
                int index;
                lock (sync)
                {
                    if (disposed || activeLoads >= maxConcurrentLoads || pending.Count == 0) return;
                    index = pending[0];
                    pending.RemoveAt(0);
                    activeLoads++;
                }

                var item = Items[index];
                var token = lifetime.Token;
                Task.Run(() => LoadAsync(item, token), token);
            }
        }

        private async Task LoadAsync(ViewerThumbnailItem item, CancellationToken token)
        {
            try
            {
                object bitmap;
                lock (sync)
                {
                    if (cache.TryGetValue(item.Path, out bitmap))
                    {
                        Touch(item.Path);
                    }
                }

                if (bitmap == null)
                {
                    bitmap = await loadThumbnail(item.Path).ConfigureAwait(false);
                    lock (sync)
                    {
                        if (disposed) return;
                        if (bitmap == null) failed.Add(item.Path);
                        else Store(item.Path, bitmap);
                    }
                }

                if (token.IsCancellationRequested || disposed) return;
                RunOnUi(() => item.SetBitmap(bitmap));
            }
            catch (Exception error)
            {
                lock (sync)
                {
                    if (!disposed) failed.Add(item.Path);
                }

                if (!token.IsCancellationRequested && !disposed)
                {
                    RunOnUi(() => item.SetPlaceholder());
                    Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "缩略图加载失败：" + item.Path, error);
                }
            }
            finally
            {
                lock (sync)
                {
                    activeLoads--;
                    queued.Remove(item.Index - 1);
                }
                Pump();
            }
        }

        // 调用方持锁。
        private void Store(string path, object bitmap)
        {
            if (cache.ContainsKey(path)) cacheOrder.Remove(path);
            cache[path] = bitmap;
            cacheOrder.AddFirst(path);
            while (cacheOrder.Count > cacheCapacity)
            {
                var oldest = cacheOrder.Last.Value;
                cacheOrder.RemoveLast();
                cache.Remove(oldest);
            }
        }

        // 调用方持锁。
        private void Touch(string path)
        {
            cacheOrder.Remove(path);
            cacheOrder.AddFirst(path);
        }

        private void RunOnUi(Action action)
        {
            var context = uiContext;
            if (context == null || ReferenceEquals(SynchronizationContext.Current, context))
            {
                action();
                return;
            }

            context.Post(state => action(), null);
        }
    }
}
