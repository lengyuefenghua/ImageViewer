using System;
using System.Collections.Generic;
using System.Globalization;
using ImageViewer.Configuration;

namespace ImageViewer.Standalone
{
    // 「复制到」配置：存 exe 旁 App.config 的 appSettings；目标按索引键保存，索引顺序即列表顺序。
    public static class CopySettingsStore
    {
        private const string TargetCountKey = "Copy.TargetCount";
        private const string TargetKeyPrefix = "Copy.Target.";
        private const string TimestampFormatKey = "Copy.TimestampFormat";
        private const string ConflictKey = "Copy.Conflict";
        private const string ShortcutKey = "Copy.Shortcut";

        public static CopySettings Load(string configPath)
        {
            var settings = new CopySettings();
            if (String.IsNullOrWhiteSpace(configPath)) return settings;

            IDictionary<string, string> values;
            try
            {
                values = AppSettingsFile.GetAll(configPath);
            }
            catch (Exception)
            {
                // 配置读取失败按默认处理，不影响复制功能。
                return settings;
            }

            settings.TimestampFormat = Get(values, TimestampFormatKey) ?? "";

            CopyConflict conflict;
            settings.Conflict = Enum.TryParse(Get(values, ConflictKey), true, out conflict)
                ? conflict
                : CopyConflict.Ask;

            var shortcut = Get(values, ShortcutKey);
            settings.Shortcut = String.IsNullOrWhiteSpace(shortcut) ? CopySettings.DefaultShortcut : shortcut;

            int count;
            if (Int32.TryParse(Get(values, TargetCountKey), out count) && count > 0)
            {
                for (var i = 0; i < count; i++)
                {
                    settings.Targets.Add(new CopyTarget
                    {
                        Path = Get(values, TargetKeyPrefix + i + ".Path") ?? "",
                        Suffix = Get(values, TargetKeyPrefix + i + ".Suffix") ?? ""
                    });
                }
            }

            return settings;
        }

        public static void Save(string configPath, CopySettings settings)
        {
            if (String.IsNullOrWhiteSpace(configPath) || settings == null) return;

            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { TimestampFormatKey, settings.TimestampFormat ?? "" },
                { ConflictKey, settings.Conflict.ToString() },
                { ShortcutKey, String.IsNullOrWhiteSpace(settings.Shortcut) ? CopySettings.DefaultShortcut : settings.Shortcut },
                { TargetCountKey, settings.Targets.Count.ToString(CultureInfo.InvariantCulture) }
            };

            for (var i = 0; i < settings.Targets.Count; i++)
            {
                values[TargetKeyPrefix + i + ".Path"] = settings.Targets[i].Path ?? "";
                values[TargetKeyPrefix + i + ".Suffix"] = settings.Targets[i].Suffix ?? "";
            }

            AppSettingsFile.Set(configPath, values);
        }

        private static string Get(IDictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : null;
        }
    }
}
