using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ImageViewer.Runtime;
using ImageViewer.Services;
using ImageViewer.ViewModels.Viewer;
using ImageViewer.Views;
using ImageViewer.Core.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace ImageViewer.Standalone
{
    // 独立查看器窗口：只做看图（视口 + 底栏），不涉及规则、Everything 与运行缓存。
    public partial class StandaloneViewerWindow : FluentWindow
    {
        private const string LoggerName = "ImageViewer";
        private const int WmNcLButtonDoubleClick = 0x00A3;
        private const int TitleBarIconSize = 24;
        private string imagePath;
        private readonly ImageViewerViewModel viewer;
        private readonly TransformGroup surfaceTransform = new TransformGroup();
        private readonly ScaleTransform surfaceScale = new ScaleTransform();
        private readonly TranslateTransform surfaceTranslate = new TranslateTransform();
        private IReadOnlyList<string> images = new string[0];
        private string windowStatePath;
        private ViewerThumbnailListViewModel thumbnailList;
        private bool syncingThumbnailSelection;
        private Point dragStart;
        private bool dragging;
        private bool isFullScreen;
        private WindowState windowStateBeforeFullScreen;
        private WindowStyle windowStyleBeforeFullScreen;
        private ResizeMode resizeModeBeforeFullScreen;

        // imagePath 允许为空：无参数启动时打开空白窗口，由右键菜单「打开图片」选择文件。
        public StandaloneViewerWindow(string imagePath)
        {
            this.imagePath = imagePath;
            viewer = new ImageViewerViewModel();
            viewer.SetEscapeAction(Close);
            DataContext = viewer;

            InitializeComponent();

            surfaceTransform.Children.Add(surfaceScale);
            surfaceTransform.Children.Add(surfaceTranslate);
            ImageSurface.RenderTransformOrigin = new Point(0, 0);
            ImageSurface.RenderTransform = surfaceTransform;
            Title = String.IsNullOrWhiteSpace(imagePath) ? "ImageViewer" : Path.GetFileName(imagePath);
            viewer.PropertyChanged += OnViewerPropertyChanged;
            ApplyTitleBarIcon();
            windowStatePath = ResolveWindowStatePath();
            RestoreWindowState();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(imagePath))
            {
                // 空白窗口：等待用户从右键菜单打开图片。
                Keyboard.Focus(this);
                return;
            }

            OpenImage(imagePath);
        }

        // 打开（或切换）一张图片：扫描同目录、建立结果集与缩略图，并定位到该图。
        private void OpenImage(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;

            imagePath = path;
            images = ViewerImageDirectoryScanner.Scan(path);
            viewer.SetResultSet(images);

            var index = 0;
            for (var candidate = 0; candidate < images.Count; candidate++)
            {
                if (String.Equals(images[candidate], path, StringComparison.OrdinalIgnoreCase))
                {
                    index = candidate;
                    break;
                }
            }

            OpenImageAt(index);
            if (thumbnailList != null)
            {
                thumbnailList.Dispose();
                thumbnailList = null;
            }
            thumbnailList = new ViewerThumbnailListViewModel(images);
            ThumbnailListBox.ItemsSource = thumbnailList.Items;
            ThumbnailPane.Visibility = thumbnailList.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            UpdateThumbnailSelection(index);
            Keyboard.Focus(this);
            Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "独立查看器已打开：" + path + "（同目录 " + images.Count + " 张）", null);
        }

        // 右键菜单「打开图片」：由用户主动选择文件后打开。
        private void OpenImageClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "打开图片",
                Filter = "图片 (*.jpg;*.png;*.bmp)|*.jpg;*.png;*.bmp|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true && !String.IsNullOrWhiteSpace(dialog.FileName))
            {
                OpenImage(dialog.FileName);
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            SaveWindowState();
            viewer.PropertyChanged -= OnViewerPropertyChanged;
            if (thumbnailList != null)
            {
                thumbnailList.Dispose();
                thumbnailList = null;
            }
            Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "独立查看器已关闭：" + imagePath, null);
            // 显式关停模式下，关闭看图窗口即结束进程。
            Application.Current.Shutdown();
        }

        private static string ResolveWindowStatePath()
        {
            return ImageViewerPaths.ConfigFilePath;
        }

        // 恢复上次窗口大小/位置/最大化；文件缺失、损坏或位置越界时保持默认（1100×800 居中）。
        private void RestoreWindowState()
        {
            var state = ViewerWindowStateStore.TryLoad(windowStatePath);
            if (state == null) return;

            if (!ViewerWindowStateStore.IsOnScreen(
                state.Left,
                state.Top,
                state.Width,
                state.Height,
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight))
            {
                // 位置越界（显示器变化）：只恢复尺寸，位置保持默认居中。
                Width = state.Width;
                Height = state.Height;
                return;
            }

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = state.Left;
            Top = state.Top;
            Width = state.Width;
            Height = state.Height;
            if (state.IsMaximized) WindowState = WindowState.Maximized;
        }

        // 保存还原尺寸（最大化时取 RestoreBounds）与最大化状态；F11 全屏状态不保存。
        private void SaveWindowState()
        {
            if (windowStatePath == null) return;
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            ViewerWindowStateStore.Save(windowStatePath, new ViewerWindowState
            {
                Width = bounds.Width,
                Height = bounds.Height,
                Left = bounds.Left,
                Top = bounds.Top,
                IsMaximized = WindowState == WindowState.Maximized
            });
        }

        private void OpenImageAt(int index)
        {
            if (index < 0 || index >= images.Count) return;

            // 目录在打开后被改名/移动/删除时，列表里的路径可能已失效：明确提示而不是显示空白。
            if (!File.Exists(images[index]))
            {
                viewer.ShowMissingFile(images[index]);
                return;
            }

            int width = 1, height = 1;
            try
            {
                var size = BitmapSourceLoader.ReadDimensions(images[index]);
                width = (int)size.Width;
                height = (int)size.Height;
            }
            catch (Exception error)
            {
                // 读取尺寸失败只影响初始视口估算，回落到 1×1 继续打开；解码失败由视图模型报错。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "独立查看器读取图片尺寸失败：" + images[index], error);
            }

            var viewportWidth = (int)ViewportHost.ActualWidth;
            var viewportHeight = (int)ViewportHost.ActualHeight;
            if (viewportWidth <= 0) viewportWidth = 800;
            if (viewportHeight <= 0) viewportHeight = 600;
            viewer.OpenAt(index, width, height, viewportWidth, viewportHeight);
        }

        private void OnViewerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (String.Equals(e.PropertyName, "FilePath", StringComparison.Ordinal))
            {
                var current = String.IsNullOrEmpty(viewer.FilePath) ? imagePath : viewer.FilePath;
                Title = Path.GetFileName(current);
                UpdateFileInfo(current);
                UpdateThumbnailSelection(viewer.CurrentPosition - 1);
                Keyboard.Focus(this);
            }

            if (String.Equals(e.PropertyName, "DisplayBitmap", StringComparison.Ordinal)
                || String.Equals(e.PropertyName, "Zoom", StringComparison.Ordinal)
                || String.Equals(e.PropertyName, "VisibleImageRectangle", StringComparison.Ordinal)
                || String.Equals(e.PropertyName, "ViewportWidth", StringComparison.Ordinal)
                || String.Equals(e.PropertyName, "ViewportHeight", StringComparison.Ordinal))
            {
                ApplyTransform();
            }
        }

        // 底栏的文件大小与修改时间来自磁盘信息；文件不可读时清空为占位（0 B / 无时间）。
        private void UpdateFileInfo(string path)
        {
            try
            {
                var info = new FileInfo(path);
                viewer.SetFileInfo(info.Exists ? info.Length : 0, info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "独立查看器读取文件信息失败：" + path, error);
            }
        }

        private void SyncViewportSize()
        {
            var width = (int)ViewportHost.ActualWidth;
            var height = (int)ViewportHost.ActualHeight;
            if (width > 0 && height > 0) viewer.SetViewportSize(width, height);
        }

        private void ViewportHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SyncViewportSize();
        }

        private void ApplyTransform()
        {
            var viewportWidth = viewer.ViewportWidth;
            var viewportHeight = viewer.ViewportHeight;
            if (viewportWidth <= 0 || viewportHeight <= 0) return;

            var zoom = viewer.Zoom;
            var scaledWidth = viewer.ImageWidth * zoom;
            var scaledHeight = viewer.ImageHeight * zoom;

            if (viewer.ImageWidth > 0) ImageSurface.Width = viewer.ImageWidth;
            if (viewer.ImageHeight > 0) ImageSurface.Height = viewer.ImageHeight;

            surfaceScale.ScaleX = zoom;
            surfaceScale.ScaleY = zoom;
            surfaceTranslate.X = scaledWidth <= viewportWidth ? (viewportWidth - scaledWidth) / 2.0 : -viewer.VisibleImageLeft * zoom;
            surfaceTranslate.Y = scaledHeight <= viewportHeight ? (viewportHeight - scaledHeight) / 2.0 : -viewer.VisibleImageTop * zoom;
        }

        private void ViewportHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var point = e.GetPosition(ViewportHost);
            var factor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
            viewer.ZoomAt(new System.Drawing.PointF((float)point.X, (float)point.Y), viewer.Zoom * factor);
            e.Handled = true;
        }

        // 缩略图列表上的滚轮 = 上一张/下一张（列表不滚动，翻页后自动滚动到当前项）。
        private void ThumbnailListMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) viewer.Previous();
            else if (e.Delta < 0) viewer.Next();
            e.Handled = true;
        }

        private void ViewportHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleFitOrActualSize();
                return;
            }

            dragStart = e.GetPosition(ViewportHost);
            dragging = ViewportHost.CaptureMouse();
        }

        private void ViewportHost_MouseMove(object sender, MouseEventArgs e)
        {
            var current = e.GetPosition(ViewportHost);
            viewer.SetPointerPosition(new System.Drawing.PointF((float)current.X, (float)current.Y));
            if (!dragging) return;

            var deltaX = current.X - dragStart.X;
            var deltaY = current.Y - dragStart.Y;
            viewer.PanBy(new System.Drawing.PointF((float)-deltaX, (float)-deltaY));
            dragStart = current;
        }

        private void ViewportHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;
            ViewportHost.ReleaseMouseCapture();
        }

        private void ViewportHost_MouseLeave(object sender, MouseEventArgs e)
        {
            // 拖拽时鼠标可短暂离开视口，保留最后坐标；非拖拽移出则清空。
            if (!dragging) viewer.ClearPointerPosition();
        }

        // 双击语义：100% 与适应窗口互切；图片尺寸恰好等于视口时两者视觉一致，可接受。
        private void ToggleFitOrActualSize()
        {
            if (Math.Abs(viewer.Zoom - 1.0) < 0.001) viewer.FitToWindow();
            else ZoomToActualSize();
        }

        private void ZoomToActualSize()
        {
            viewer.ZoomAt(
                new System.Drawing.PointF((float)(ViewportHost.ActualWidth / 2), (float)(ViewportHost.ActualHeight / 2)),
                1.0);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CopyCurrentFile();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.T && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == ModifierKeys.None)
            {
                ToggleThumbnails();
                e.Handled = true;
                return;
            }

            viewer.HandleKey(e.Key.ToString());
            e.Handled = e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Escape;
        }

        // 翻页与缩略图选中互相同步：用标志防止 SelectedIndex 赋值回环触发切换。
        private void UpdateThumbnailSelection(int index)
        {
            if (thumbnailList == null || index < 0 || index >= thumbnailList.Items.Count) return;
            thumbnailList.SetCurrentIndex(index);
            syncingThumbnailSelection = true;
            try
            {
                ThumbnailListBox.SelectedIndex = index;
                ThumbnailListBox.ScrollIntoView(ThumbnailListBox.SelectedItem);
            }
            finally
            {
                syncingThumbnailSelection = false;
            }
        }

        private void ThumbnailSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (syncingThumbnailSelection || thumbnailList == null) return;
            var index = ThumbnailListBox.SelectedIndex;
            if (index < 0 || index == viewer.CurrentPosition - 1) return;
            OpenImageAt(index);
        }

        private void ToggleThumbnails()
        {
            if (thumbnailList == null) return;
            thumbnailList.ToggleVisibility();
            ThumbnailPane.Visibility = thumbnailList.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleThumbnailsClick(object sender, RoutedEventArgs e)
        {
            ToggleThumbnails();
        }

        // Ctrl+C：复制当前图片文件本身（文件引用），可在资源管理器中直接粘贴。
        private void CopyCurrentFile()
        {
            var path = String.IsNullOrEmpty(viewer.FilePath) ? imagePath : viewer.FilePath;
            try
            {
                Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { path });
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "独立查看器已复制图片文件：" + path, null);
            }
            catch (Exception error)
            {
                // 剪贴板可能被其他进程占用，失败不影响看图。
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "独立查看器复制图片文件失败：" + path, error);
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            var source = HwndSource.FromHwnd(handle);
            if (source != null) source.AddHook(WindowMessageHook);
        }

        // 标题栏属于非客户区，WPF 收不到双击；在窗口级消息钩子里拦截 NC 双击实现全屏。
        private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == WmNcLButtonDoubleClick)
            {
                ToggleFullScreen();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void ToggleFullScreen()
        {
            if (isFullScreen)
            {
                WindowStyle = windowStyleBeforeFullScreen;
                ResizeMode = resizeModeBeforeFullScreen;
                WindowState = windowStateBeforeFullScreen;
                ViewerTitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                windowStyleBeforeFullScreen = WindowStyle;
                resizeModeBeforeFullScreen = ResizeMode;
                windowStateBeforeFullScreen = WindowState;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
                // 自定义标题栏属于窗口内容，WindowStyle=None 不会隐藏它，必须手动折叠。
                ViewerTitleBar.Visibility = Visibility.Collapsed;
            }

            isFullScreen = !isFullScreen;
            FullScreenMenuItem.Header = isFullScreen ? "退出全屏" : "全屏";
            Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, isFullScreen ? "独立查看器进入全屏" : "独立查看器退出全屏", null);
        }

        private void CopyFileClick(object sender, RoutedEventArgs e)
        {
            CopyCurrentFile();
        }

        private void FitClick(object sender, RoutedEventArgs e)
        {
            viewer.FitToWindow();
        }

        private void ActualSizeClick(object sender, RoutedEventArgs e)
        {
            ZoomToActualSize();
        }

        private void FullScreenClick(object sender, RoutedEventArgs e)
        {
            ToggleFullScreen();
        }

        private void ExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenSettingsClick(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow { Owner = this };
            settings.ShowDialog();
        }

        private void CopyPathClick(object sender, RoutedEventArgs e)
        {
            var path = String.IsNullOrEmpty(viewer.FilePath) ? imagePath : viewer.FilePath;
            try
            {
                Clipboard.SetText(path);
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "独立查看器已复制图片路径：" + path, null);
            }
            catch (Exception error)
            {
                // 剪贴板可能被其他进程占用，失败不影响看图。
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "独立查看器复制图片路径失败：" + path, error);
            }
        }

        private void RevealInExplorerClick(object sender, RoutedEventArgs e)
        {
            var path = String.IsNullOrEmpty(viewer.FilePath) ? imagePath : viewer.FilePath;
            try
            {
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "独立查看器在资源管理器中显示失败：" + path, error);
            }
        }

        private void ApplyTitleBarIcon()
        {
            try
            {
                var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
                if (scale <= 0) scale = 1.0;
                var target = Math.Max(1, (int)Math.Round(TitleBarIconSize * scale));
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    new Uri("pack://application:,,,/ImageViewer.ico"),
                    System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                var frame = decoder.Frames
                    .OrderBy(f => Math.Abs(f.PixelWidth - target))
                    .First();
                if (frame.CanFreeze) frame.Freeze();
                AppIcon.Source = frame;
            }
            catch (Exception error)
            {
                // 图标是可选装饰，取不到时不阻塞看图。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "独立查看器设置标题栏图标失败（可选装饰）。", error);
            }
        }
    }
}
