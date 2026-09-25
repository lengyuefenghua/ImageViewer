using System;
using System.Collections.Generic;

namespace ImageViewer.Runtime
{
    // 查看器偏好（目前仅首启默认查看器引导的拒绝标记），与窗口状态共存于 exe 旁的 App.config。
    public static class ViewerPreferencesStore
    {
        private const string LoggerName = "ImageViewer";
        private const string DismissedKey = "Preferences.DefaultViewerPromptDismissed";

        public static bool TryLoadDismissed(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string value;
                bool parsed;
                return AppSettingsFile.GetAll(path).TryGetValue(DismissedKey, out value)
                    && Boolean.TryParse(value, out parsed)
                    && parsed;
            }
            catch (Exception error)
            {
                // 文件缺失或损坏都按「未拒绝」处理，不影响看图。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "读取查看器偏好失败，使用默认：" + path, error);
                return false;
            }
        }

        public static void SaveDismissed(string path, bool dismissed)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            try
            {
                AppSettingsFile.Set(path, new Dictionary<string, string>
                {
                    { DismissedKey, dismissed ? "true" : "false" }
                });
            }
            catch (Exception error)
            {
                // 偏好保存失败不影响看图与关闭流程。
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "保存查看器偏好失败：" + path, error);
            }
        }
    }
}
