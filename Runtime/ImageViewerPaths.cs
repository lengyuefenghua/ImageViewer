using System;
using System.IO;
using Microsoft.Win32;

namespace ImageViewer.Runtime
{
    // ImageViewer 的运行时路径：每用户配置与日志均存放于 %AppData%\ImageViewer。
    public sealed class ImageViewerPaths
    {
        private const string AppFolderName = "ImageViewer";
        private const string ConfigFileName = "settings.config";
        private const string LegacyConfigFileName = "ImageViewer.exe.config";
        private const string InstallerAppId = "{E4A60292-839A-4C2B-B811-C22BBD3AA634}";

        private ImageViewerPaths(string appDataDirectory)
        {
            AppDataDirectory = appDataDirectory;
            LogsDirectory = Path.Combine(appDataDirectory, "Logs");
        }

        public string AppDataDirectory { get; private set; }
        public string LogsDirectory { get; private set; }

        // 设置文件位于每用户可写的数据目录；只在尚无新配置时迁移旧版本的 exe 旁配置。
        public static string ConfigFilePath
        {
            get { return Path.Combine(ForCurrentUser().AppDataDirectory, ConfigFileName); }
        }

        public static void MigrateLegacyConfig()
        {
            var current = ConfigFilePath;
            if (File.Exists(current)) return;

            try
            {
                var legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LegacyConfigFileName);
                if (!File.Exists(legacy))
                {
                    legacy = FindPreviousInstallConfig();
                    if (legacy == null) return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(current));
                File.Copy(legacy, current);
                Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "Legacy user configuration migrated", null);
            }
            catch (IOException error)
            {
                if (File.Exists(current)) return;
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "Failed to migrate legacy user configuration", error);
                throw;
            }
            catch (UnauthorizedAccessException error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "Cannot migrate legacy user configuration because the target directory is not writable", error);
                throw;
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "Failed to migrate legacy user configuration", error);
                throw;
            }
        }

        private static string FindPreviousInstallConfig()
        {
            var userConfig = FindPreviousInstallConfig(Registry.CurrentUser);
            if (userConfig != null) return userConfig;
            return FindPreviousInstallConfig(Registry.LocalMachine);
        }

        private static string FindPreviousInstallConfig(RegistryKey hive)
        {
            using (var uninstallRoot = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", false))
            {
                if (uninstallRoot == null) return null;
                foreach (var subKeyName in uninstallRoot.GetSubKeyNames())
                {
                    if (!String.Equals(subKeyName, InstallerAppId + "_is1", StringComparison.OrdinalIgnoreCase)) continue;
                    using (var key = uninstallRoot.OpenSubKey(subKeyName, false))
                    {
                        var installLocation = key == null ? null : key.GetValue("InstallLocation") as string;
                        if (String.IsNullOrWhiteSpace(installLocation)) continue;

                        var candidate = Path.Combine(installLocation, LegacyConfigFileName);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            return null;
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
