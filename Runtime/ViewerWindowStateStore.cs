using System;
using System.Collections.Generic;
using System.Globalization;
using ImageViewer.Runtime;

namespace ImageViewer.Runtime
{
    // 查看器窗口偏好（大小/位置/最大化）：属用户配置。
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
        private const string WidthKey = "Window.Width";
        private const string HeightKey = "Window.Height";
        private const string LeftKey = "Window.Left";
        private const string TopKey = "Window.Top";
        private const string MaximizedKey = "Window.IsMaximized";

        public static ViewerWindowState TryLoad(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var values = AppSettingsFile.GetAll(path);
                var width = ReadDouble(values, WidthKey);
                var height = ReadDouble(values, HeightKey);
                if (width == null || height == null || width <= 0 || height <= 0) return null;

                return new ViewerWindowState
                {
                    Width = width.Value,
                    Height = height.Value,
                    Left = ReadDouble(values, LeftKey) ?? 0,
                    Top = ReadDouble(values, TopKey) ?? 0,
                    IsMaximized = ReadBool(values, MaximizedKey)
                };
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
                AppSettingsFile.Set(path, new Dictionary<string, string>
                {
                    { WidthKey, state.Width.ToString(CultureInfo.InvariantCulture) },
                    { HeightKey, state.Height.ToString(CultureInfo.InvariantCulture) },
                    { LeftKey, state.Left.ToString(CultureInfo.InvariantCulture) },
                    { TopKey, state.Top.ToString(CultureInfo.InvariantCulture) },
                    { MaximizedKey, state.IsMaximized ? "true" : "false" }
                });
            }
            catch (Exception error)
            {
                // 窗口偏好保存失败不影响看图与关闭流程。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "保存查看器窗口状态失败：" + path, error);
            }
        }

        private static double? ReadDouble(IDictionary<string, string> values, string key)
        {
            string value;
            double parsed;
            if (values.TryGetValue(key, out value)
                && Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            return null;
        }

        private static bool ReadBool(IDictionary<string, string> values, string key)
        {
            string value;
            bool parsed;
            return values.TryGetValue(key, out value) && Boolean.TryParse(value, out parsed) && parsed;
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
