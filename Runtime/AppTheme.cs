using System;
using System.Collections.Generic;

namespace ImageViewer.Runtime
{
    public enum AppTheme
    {
        Dark,
        Light,
        System
    }

    // 外观主题配置：默认深色，存 exe 旁 App.config 的 appSettings。
    public static class AppThemeStore
    {
        public const string ThemeKey = "Appearance.Theme";

        public static AppTheme Resolve(IDictionary<string, string> settings)
        {
            string value;
            AppTheme parsed;
            if (settings != null
                && settings.TryGetValue(ThemeKey, out value)
                && Enum.TryParse(value, true, out parsed)
                && Enum.IsDefined(typeof(AppTheme), parsed))
            {
                return parsed;
            }
            return AppTheme.Dark;
        }

        public static AppTheme Load(string configPath)
        {
            try
            {
                return Resolve(AppSettingsFile.GetAll(configPath));
            }
            catch (Exception)
            {
                // 配置读取失败按默认深色处理。
                return AppTheme.Dark;
            }
        }

        public static void Save(string configPath, AppTheme theme)
        {
            if (String.IsNullOrWhiteSpace(configPath)) return;
            AppSettingsFile.Set(configPath, new Dictionary<string, string>
            {
                { ThemeKey, theme.ToString() }
            });
        }
    }
}
