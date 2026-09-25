using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace ImageViewer.Runtime
{
    // 通过 .NET 内置 ConfigurationManager 读写标准 App.config 的 appSettings 段。
    // 用映射路径而非默认 exe 配置，便于测试与显式定位；appSettings 读改写非原子，进程内用静态锁串行化。
    public static class AppSettingsFile
    {
        private static readonly object SyncRoot = new object();
        private const string EmptyConfigContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration />";

        public static IDictionary<string, string> GetAll(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return values;

            var configuration = Open(path);
            foreach (KeyValueConfigurationElement element in configuration.AppSettings.Settings)
            {
                values[element.Key] = element.Value;
            }
            return values;
        }

        public static void Set(string path, IDictionary<string, string> values)
        {
            if (String.IsNullOrWhiteSpace(path) || values == null || values.Count == 0) return;

            lock (SyncRoot)
            {
                EnsureFileExists(path);
                var configuration = Open(path);
                foreach (var pair in values)
                {
                    var setting = configuration.AppSettings.Settings[pair.Key];
                    if (setting == null) configuration.AppSettings.Settings.Add(pair.Key, pair.Value);
                    else setting.Value = pair.Value;
                }
                configuration.Save(ConfigurationSaveMode.Minimal);
            }
        }

        private static System.Configuration.Configuration Open(string path)
        {
            var map = new ExeConfigurationFileMap { ExeConfigFilename = path };
            return ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
        }

        // OpenMappedExeConfiguration 需要文件存在：缺失时先写一个最小合法的 config 骨架。
        private static void EnsureFileExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (!File.Exists(path)) File.WriteAllText(path, EmptyConfigContent);
        }
    }
}
