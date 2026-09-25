using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageViewer.Runtime;
using ImageViewer.Imaging.Viewport;
using ImageViewer.Services;

namespace ImageViewer.Views
{
    // 看图视图模型：只承载结果集翻页、视口、底栏信息与指针取色。
    public sealed class ImageViewerViewModel : BindableViewModel
    {
        private readonly Func<string, Size> imageSizeLoader;
        private readonly SynchronizationContext uiContext;
        // 主图解码闸门：同时最多解码 1 张，避免快速翻页时多张大图并发解码造成内存峰值。
        private readonly SemaphoreSlim decodeGate = new SemaphoreSlim(1, 1);
        // 指针取色的复用缓冲，避免鼠标移动时反复分配。
        private readonly byte[] pixelBuffer = new byte[4];
        private long loadGeneration;
        private Action escape;
        private ViewportState viewport;
        private IReadOnlyList<string> resultSet = new List<string>();
        private int currentIndex = -1;
        private int imageWidth;
        private int imageHeight;
        private int viewportWidth;
        private int viewportHeight;
        private long imageSize;
        private DateTime imageModifiedUtc = DateTime.MinValue;

        private Rectangle displayedImageRectangle = Rectangle.Empty;
        private int pointerX = -1;
        private int pointerY = -1;
        private int pixelR = -1;
        private int pixelG = -1;
        private int pixelB = -1;

        public ImageViewerViewModel(Func<string, Size> imageSizeLoader = null, Action escape = null)
        {
            this.imageSizeLoader = imageSizeLoader;
            this.escape = escape;
            var context = SynchronizationContext.Current;
            uiContext = context is System.Windows.Threading.DispatcherSynchronizationContext ? context : null;
        }

        public string FilePath { get; private set; }
        public object DisplayBitmap { get; private set; }
        public double Zoom { get { return viewport == null ? 1.0 : viewport.Zoom; } }
        public int ZoomPercent { get { return (int)Math.Round(Zoom * 100); } }
        public int PointerX { get { return pointerX; } }
        public int PointerY { get { return pointerY; } }
        public int PixelR { get { return pixelR; } }
        public int PixelG { get { return pixelG; } }
        public int PixelB { get { return pixelB; } }
        public int ImageWidth { get { return imageWidth; } }
        public int ImageHeight { get { return imageHeight; } }
        // 状态栏尺寸字段：位深在解码完成前不可知，此时只显示宽×高。
        public string ImageDimensionsWithDepth
        {
            get
            {
                if (imageWidth <= 0 || imageHeight <= 0) return null;
                var depth = ImageBitDepth;
                return depth <= 0
                    ? imageWidth + " × " + imageHeight
                    : imageWidth + " × " + imageHeight + " × " + depth + " BPP";
            }
        }
        // 解码位图的理论字节数（宽×高×每像素字节），与文件大小并列显示。
        public string ImageDecodedSizeDisplay
        {
            get
            {
                var depth = ImageBitDepth;
                if (imageWidth <= 0 || imageHeight <= 0 || depth <= 0) return null;
                return FormatSize((long)imageWidth * imageHeight * ((depth + 7) / 8));
            }
        }
        public string ImageSizeDisplay
        {
            get { return FormatSize(imageSize); }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024) return (bytes / 1024.0).ToString("0.00") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("0.00") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
        }
        public string ImageModifiedDisplay
        {
            get
            {
                return imageModifiedUtc == DateTime.MinValue
                    ? null
                    : imageModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        public int ImageBitDepth
        {
            get
            {
                var source = DisplayBitmap as BitmapSource;
                return source == null ? 0 : source.Format.BitsPerPixel;
            }
        }
        public string ErrorMessage { get; private set; }
        public bool IsLoading { get; private set; }
        internal Task DecodeTask { get; private set; }
        public Rectangle VisibleImageRectangle { get { return viewport == null ? Rectangle.Empty : viewport.VisibleImageRectangle; } }
        public double VisibleImageLeft { get { return viewport == null ? 0.0 : viewport.Left; } }
        public double VisibleImageTop { get { return viewport == null ? 0.0 : viewport.Top; } }
        public int CurrentPosition { get { return currentIndex < 0 ? 0 : currentIndex + 1; } }
        public int ResultCount { get { return resultSet.Count; } }
        public int ViewportWidth { get { return viewportWidth; } private set { viewportWidth = value; } }
        public int ViewportHeight { get { return viewportHeight; } private set { viewportHeight = value; } }

        public void SetResultSet(IReadOnlyList<string> paths)
        {
            if (paths == null) throw new ArgumentNullException("paths");

            resultSet = new List<string>(paths);
            currentIndex = -1;
            Changed("CurrentPosition");
            Changed("ResultCount");
        }

        public void OpenAt(
            int index,
            int imageWidth,
            int imageHeight,
            int viewportWidth,
            int viewportHeight)
        {
            if (index < 0 || index >= resultSet.Count) throw new ArgumentOutOfRangeException("index");

            currentIndex = index;
            Open(resultSet[index], imageWidth, imageHeight, viewportWidth, viewportHeight);
            Changed("CurrentPosition");
        }

        public async Task PreviousAsync()
        {
            if (currentIndex <= 0) return;
            await OpenResultAtAsync(currentIndex - 1);
        }

        public async Task NextAsync()
        {
            if (currentIndex < 0 || currentIndex >= resultSet.Count - 1) return;
            await OpenResultAtAsync(currentIndex + 1);
        }

        // 翻页读取尺寸放到线程池：网络盘/慢盘读图片头不再阻塞 UI。
        private async Task OpenResultAtAsync(int index)
        {
            // 文件已被改名/移动/删除：更新位置并给出提示，不显示误导的 1×1 尺寸与巨大缩放。
            if (!File.Exists(resultSet[index]))
            {
                currentIndex = index;
                ShowMissingFile(resultSet[index]);
                Changed("CurrentPosition");
                return;
            }

            int width = 1, height = 1;
            try
            {
                var size = await ReadDimensionsAsync(resultSet[index]);
                width = (int)size.Width;
                height = (int)size.Height;
            }
            catch (Exception error)
            {
                // 读取尺寸失败仅影响初始视口估算，回落到 1×1 继续打开。
                Diagnostics.Sink.Log(LogSeverity.Debug, "ImageViewer", "读取图片尺寸失败（结果集），回落到默认尺寸：" + resultSet[index], error);
            }

            OpenAt(index, width, height, viewportWidth, viewportHeight);
        }

        // 测试可注入同步 loader；生产在后台线程读取（BitmapSourceLoader 返回 WPF Size，这里统一为 System.Drawing.Size）。
        private Task<Size> ReadDimensionsAsync(string path)
        {
            if (imageSizeLoader != null) return Task.FromResult(imageSizeLoader(path));
            return Task.Run(() =>
            {
                var size = BitmapSourceLoader.ReadDimensions(path);
                return new Size((int)size.Width, (int)size.Height);
            });
        }

        public async Task HandleKeyAsync(string key)
        {
            if (String.Equals(key, "Left", StringComparison.Ordinal)) await PreviousAsync();
            else if (String.Equals(key, "Right", StringComparison.Ordinal)) await NextAsync();
            else if (String.Equals(key, "Escape", StringComparison.Ordinal)) { var action = escape; if (action != null) action(); }
        }

        public void Open(
            string path,
            int imageWidth,
            int imageHeight,
            int viewportWidth,
            int viewportHeight)
        {
            try
            {
                if (imageSizeLoader != null)
                {
                    var size = imageSizeLoader(path);
                    imageWidth = size.Width;
                    imageHeight = size.Height;
                }

                var effectiveViewportWidth = this.viewportWidth > 0 ? this.viewportWidth : viewportWidth;
                var effectiveViewportHeight = this.viewportHeight > 0 ? this.viewportHeight : viewportHeight;

                var sameSize = viewport != null
                    && viewport.ImageWidth == imageWidth
                    && viewport.ImageHeight == imageHeight;

                if (sameSize)
                {
                    viewport.SetViewportSize(effectiveViewportWidth, effectiveViewportHeight);
                }
                else
                {
                    viewport = new ViewportState(
                        imageWidth,
                        imageHeight,
                        effectiveViewportWidth,
                        effectiveViewportHeight,
                        ViewportInitialZoom.Fit);
                    DisplayBitmap = null;
                }

                this.imageWidth = imageWidth;
                this.imageHeight = imageHeight;
                displayedImageRectangle = new Rectangle(0, 0, imageWidth, imageHeight);
                ViewportWidth = effectiveViewportWidth;
                ViewportHeight = effectiveViewportHeight;
                FilePath = path;
                ErrorMessage = null;

                var generation = ++loadGeneration;
                if (File.Exists(path))
                {
                    IsLoading = true;
                    var decodeWidth = imageWidth;
                    DecodeTask = DecodeAsync(path, decodeWidth, imageHeight, generation);
                }
                else
                {
                    IsLoading = false;
                    DecodeTask = null;
                }
            }
            catch (Exception error)
            {
                FilePath = path;
                ErrorMessage = error.Message;
                displayedImageRectangle = Rectangle.Empty;
                this.imageWidth = 0;
                this.imageHeight = 0;
                DisplayBitmap = null;
                IsLoading = false;
                // 打开主图失败是关键路径，必须带图片路径记录。
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "打开图片失败：" + path, error);
            }

            Changed("FilePath");
            Changed("DisplayBitmap");
            Changed("Zoom");
            Changed("ZoomPercent");
            Changed("VisibleImageRectangle");
            Changed("ErrorMessage");
            Changed("ImageDimensionsWithDepth");
            Changed("ImageDecodedSizeDisplay");
            Changed("ImageBitDepth");
            Changed("IsLoading");
        }

        // 解码闸门：同时最多 1 张主图在解码；排队期间被更新请求取代的直接跳过，避免多张大图叠加占内存。
        private async Task DecodeAsync(string path, int decodeWidth, int imageHeight, long generation)
        {
            await decodeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (generation != Interlocked.Read(ref loadGeneration))
                {
                    Diagnostics.Sink.Log(LogSeverity.Debug, "ImageViewer", "过期解码请求跳过：" + path, null);
                    return;
                }
                var bitmap = await Task.Run(() => BitmapSourceLoader.LoadForDisplay(path, decodeWidth, imageHeight)).ConfigureAwait(false);
                RunOnUi(() => CompleteDecodeSuccess(path, generation, bitmap));
            }
            catch (Exception error)
            {
                RunOnUi(() => CompleteDecodeFailure(path, generation, error));
            }
            finally
            {
                decodeGate.Release();
            }
        }

        private void CompleteDecodeSuccess(string path, long generation, object bitmap)
        {
            if (generation != loadGeneration || !String.Equals(path, FilePath, StringComparison.Ordinal)) return;

            DisplayBitmap = bitmap;
            Changed("DisplayBitmap");
            Changed("ImageBitDepth");
            Changed("ImageDimensionsWithDepth");
            Changed("ImageDecodedSizeDisplay");

            IsLoading = false;
            Changed("IsLoading");
        }

        private void CompleteDecodeFailure(string path, long generation, Exception error)
        {
            if (generation != loadGeneration || !String.Equals(path, FilePath, StringComparison.Ordinal)) return;

            var message = error == null ? "图片解码失败。" : error.GetBaseException().Message;
            // 主图解码失败是关键路径：必须带图片路径记 Error（显示占位图，进程不崩溃）。
            Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "图片解码失败：" + path, error);
            ErrorMessage = message;
            DisplayBitmap = null;
            displayedImageRectangle = Rectangle.Empty;
            imageWidth = 0;
            imageHeight = 0;
            Changed("ErrorMessage");
            Changed("DisplayBitmap");
            Changed("ImageDimensionsWithDepth");
            Changed("ImageDecodedSizeDisplay");

            IsLoading = false;
            Changed("IsLoading");
        }

        private void RunOnUi(Action action)
        {
            var context = uiContext;
            if (context == null || ReferenceEquals(SynchronizationContext.Current, context))
            {
                action();
                return;
            }

            context.Send(state => action(), null);
        }

        // 文件已被改名/移动/删除：清空位图与视口并给出提示，避免显示误导的 1×1 尺寸与巨大缩放。
        public void ShowMissingFile(string path)
        {
            loadGeneration++;
            FilePath = path;
            viewport = null;
            imageWidth = 0;
            imageHeight = 0;
            DisplayBitmap = null;
            displayedImageRectangle = Rectangle.Empty;
            ErrorMessage = "文件打开失败：" + path + "，请确认文件是否存在";
            IsLoading = false;
            DecodeTask = null;
            Changed("FilePath");
            Changed("DisplayBitmap");
            Changed("Zoom");
            Changed("ZoomPercent");
            Changed("VisibleImageRectangle");
            Changed("ErrorMessage");
            Changed("ImageDimensionsWithDepth");
            Changed("ImageDecodedSizeDisplay");
            Changed("ImageBitDepth");
            Changed("IsLoading");
        }

        public void ClearDisplay()
        {
            loadGeneration++;
            DecodeTask = null;
            IsLoading = false;
            DisplayBitmap = null;
            viewport = null;
            displayedImageRectangle = Rectangle.Empty;
            imageWidth = 0;
            imageHeight = 0;
            Changed("IsLoading");
            Changed("DisplayBitmap");
            Changed("ImageDimensionsWithDepth");
            Changed("ImageDecodedSizeDisplay");
            Changed("Zoom");
            Changed("ZoomPercent");
            Changed("VisibleImageRectangle");
        }

        public void SetFileInfo(long size, DateTime modifiedUtc)
        {
            imageSize = size;
            imageModifiedUtc = modifiedUtc;
            Changed("ImageSizeDisplay");
            Changed("ImageModifiedDisplay");
        }

        public void SetViewportSize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;

            ViewportWidth = width;
            ViewportHeight = height;
            if (viewport == null) return;

            // 窗口尺寸变化总是重新适配（含手动缩放/平移之后）。
            viewport.SetViewportSize(width, height);
            Changed("ViewportWidth");
            Changed("ViewportHeight");
            FitToWindow();
        }

        public void FitToWindow()
        {
            if (viewport == null) return;
            viewport = new ViewportState(
                viewport.ImageWidth,
                viewport.ImageHeight,
                viewport.ViewportWidth,
                viewport.ViewportHeight,
                ViewportInitialZoom.Fit);
            Changed("Zoom");
            Changed("ZoomPercent");
            Changed("VisibleImageRectangle");
        }

        public void ZoomAt(PointF point, double zoom)
        {
            if (viewport == null) return;
            viewport.ZoomAt(point, zoom);
            Changed("Zoom");
            Changed("ZoomPercent");
            Changed("VisibleImageRectangle");
        }

        public PointF ImagePointAt(PointF point)
        {
            return viewport == null ? PointF.Empty : viewport.ImagePointAt(point);
        }

        public void PanBy(PointF delta)
        {
            if (viewport == null) return;
            viewport.PanBy(delta);
            Changed("VisibleImageRectangle");
        }

        public void SetPointerPosition(PointF point)
        {
            var imagePoint = ImagePointAt(point);
            if (imagePoint == PointF.Empty || !IsInsideImage(imagePoint))
            {
                SetPointerPlaceholder();
                return;
            }

            pointerX = (int)Math.Floor(imagePoint.X);
            pointerY = (int)Math.Floor(imagePoint.Y);
            int r, g, b;
            if (TrySampleRgb(DisplayBitmap as BitmapSource, displayedImageRectangle, imagePoint, out r, out g, out b))
            {
                pixelR = r;
                pixelG = g;
                pixelB = b;
            }
            else
            {
                pixelR = -1;
                pixelG = -1;
                pixelB = -1;
            }

            NotifyPointerChanged();
        }

        // 指针不在图像上（或移出窗口）时清空坐标与取色。
        public void ClearPointerPosition()
        {
            SetPointerPlaceholder();
        }

        private void SetPointerPlaceholder()
        {
            pointerX = -1;
            pointerY = -1;
            pixelR = -1;
            pixelG = -1;
            pixelB = -1;
            NotifyPointerChanged();
        }

        private void NotifyPointerChanged()
        {
            Changed("PointerX");
            Changed("PointerY");
            Changed("PixelR");
            Changed("PixelG");
            Changed("PixelB");
        }

        // 原图坐标必须落在图像范围内，否则视为不在图像上（显示占位）。
        private bool IsInsideImage(PointF imagePoint)
        {
            return imageWidth > 0 && imageHeight > 0
                && imagePoint.X >= 0 && imagePoint.Y >= 0
                && imagePoint.X < imageWidth && imagePoint.Y < imageHeight;
        }

        internal bool TrySampleRgb(BitmapSource source, Rectangle displayedRectangle, PointF imagePoint, out int r, out int g, out int b)
        {
            r = -1;
            g = -1;
            b = -1;
            if (source == null || displayedRectangle.Width <= 0 || displayedRectangle.Height <= 0) return false;

            var pixelX = (int)Math.Floor((imagePoint.X - displayedRectangle.X) / displayedRectangle.Width * source.PixelWidth);
            var pixelY = (int)Math.Floor((imagePoint.Y - displayedRectangle.Y) / displayedRectangle.Height * source.PixelHeight);
            if (pixelX < 0 || pixelY < 0 || pixelX >= source.PixelWidth || pixelY >= source.PixelHeight) return false;

            try
            {
                var converted = source.Format == PixelFormats.Bgra32
                    ? source
                    : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                converted.CopyPixels(new System.Windows.Int32Rect(pixelX, pixelY, 1, 1), pixelBuffer, 4, 0);
                b = pixelBuffer[0];
                g = pixelBuffer[1];
                r = pixelBuffer[2];
                return true;
            }
            catch (Exception error)
            {
                // 单像素采样失败属诊断细节，不影响主流程。
                Diagnostics.Sink.Log(LogSeverity.Debug, "ImageViewer", "取色失败，无法读取像素值。", error);
                return false;
            }
        }

        public void SetEscapeAction(Action action)
        {
            escape = action;
        }
    }
}
