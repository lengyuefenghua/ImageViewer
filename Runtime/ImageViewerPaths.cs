using System;
using System.IO;

namespace ImageViewer.Runtime
{
    // ImageViewer 的运行时路径：日志固定落 %AppData%\ImageViewer；用户配置固定在 exe 旁的 App.config。
    public sealed class ImageViewerPaths
    {
        private const string AppFolderName = "ImageViewer";
        private const string ConfigFileName = "ImageViewer.exe.config";

        private ImageViewerPaths(string appDataDirectory)
        {
            AppDataDirectory = appDataDirectory;
            LogsDirectory = Path.Combine(appDataDirectory, "Logs");
        }

        public string AppDataDirectory { get; private set; }
        public string LogsDirectory { get; private set; }

        // exe 同目录的 App.config：窗口状态与首启偏好都存这里。
        public static string ConfigFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName); }
        }

        public static ImageViewerPaths ForCurrentUser()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);
            return new ImageViewerPaths(root);
        }
    }
}
