using System;
using System.IO;

namespace ImageViewer.App.Runtime
{
    // ImageViewer 的运行时目录：数据固定落在 %AppData%\ImageViewer，与主程序 ImageDataViewer 完全隔离。
    public sealed class ImageViewerPaths
    {
        private const string AppFolderName = "ImageViewer";

        private ImageViewerPaths(string appDataDirectory)
        {
            AppDataDirectory = appDataDirectory;
            ConfigurationDirectory = Path.Combine(appDataDirectory, "Configuration");
            LogsDirectory = Path.Combine(appDataDirectory, "Logs");
        }

        public string AppDataDirectory { get; private set; }
        public string ConfigurationDirectory { get; private set; }
        public string LogsDirectory { get; private set; }

        public static ImageViewerPaths ForCurrentUser()
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);
            return new ImageViewerPaths(root);
        }
    }
}
