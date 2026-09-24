using System;
using System.IO;
using ImageViewer.App.Configuration;
using ImageViewer.Core.Diagnostics;
using ImageViewer.Services;

namespace ImageViewer.App.Runtime
{
    // 查看器偏好（目前仅首启默认查看器引导的拒绝标记）：属用户配置，不是运行缓存。
    public sealed class ViewerPreferences
    {
        public bool DefaultViewerPromptDismissed { get; set; }
    }

    public static class ViewerPreferencesStore
    {
        private const string LoggerName = "ImageViewer";
        public const string FileName = "settings.json";

        public static bool TryLoadDismissed(string path)
        {
            var preferences = TryLoad(path);
            return preferences != null && preferences.DefaultViewerPromptDismissed;
        }

        public static ViewerPreferences TryLoad(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (!File.Exists(path)) return null;
                return JsonSerialization.Deserialize<ViewerPreferences>(File.ReadAllText(path));
            }
            catch (Exception error)
            {
                // 文件缺失或损坏都按「未拒绝」处理，不影响看图。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "读取查看器偏好失败，使用默认：" + path, error);
                return null;
            }
        }

        public static void SaveDismissed(string path, bool dismissed)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                AtomicJsonFile.Save(
                    path,
                    JsonSerialization.Serialize(new ViewerPreferences { DefaultViewerPromptDismissed = dismissed }));
            }
            catch (Exception error)
            {
                // 偏好保存失败不影响看图与关闭流程。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "保存查看器偏好失败：" + path, error);
            }
        }
    }
}
