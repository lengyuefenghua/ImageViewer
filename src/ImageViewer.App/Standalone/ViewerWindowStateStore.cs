using System;
using System.IO;
using ImageViewer.App.Configuration;
using ImageViewer.Core.Diagnostics;
using ImageViewer.Services;

namespace ImageViewer.App.Standalone
{
    // 独立查看器的窗口偏好（大小/位置/最大化）：属用户配置，不是运行缓存。
    public sealed class ViewerWindowState
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool IsMaximized { get; set; }
    }

    public static class ViewerWindowStateStore
    {
        private const string LoggerName = "ImageViewer";
        public const string FileName = "viewer-window.json";

        public static ViewerWindowState TryLoad(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (!File.Exists(path)) return null;
                var state = JsonSerialization.Deserialize<ViewerWindowState>(File.ReadAllText(path));
                if (state == null || state.Width <= 0 || state.Height <= 0) return null;
                return state;
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "读取查看器窗口状态失败，使用默认：" + path, error);
                return null;
            }
        }

        public static void Save(string path, ViewerWindowState state)
        {
            if (String.IsNullOrWhiteSpace(path) || state == null) return;
            try
            {
                AtomicJsonFile.Save(path, JsonSerialization.Serialize(state));
            }
            catch (Exception error)
            {
                // 窗口偏好保存失败不影响看图与关闭流程。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "保存查看器窗口状态失败：" + path, error);
            }
        }

        // 窗口至少部分落在虚拟屏幕内才认为位置有效（显示器变化时回退居中）。
        internal static bool IsOnScreen(
            double left,
            double top,
            double width,
            double height,
            double virtualLeft,
            double virtualTop,
            double virtualWidth,
            double virtualHeight)
        {
            if (width <= 0 || height <= 0) return false;
            return left + width > virtualLeft
                && left < virtualLeft + virtualWidth
                && top + height > virtualTop
                && top < virtualTop + virtualHeight;
        }
    }
}
