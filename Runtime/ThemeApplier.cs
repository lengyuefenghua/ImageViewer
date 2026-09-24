using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ImageViewer.Runtime
{
    // 把 AppTheme 应用到 Wpf.Ui 主题系统；System 时订阅系统深浅色变化实时跟随。
    public static class ThemeApplier
    {
        public static void Apply(AppTheme theme)
        {
            if (theme == AppTheme.Light) ApplicationThemeManager.Apply(ApplicationTheme.Light);
            else if (theme == AppTheme.System) ApplicationThemeManager.ApplySystemTheme();
            else ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }

        // follow=true 时跟随系统主题变化；false 时取消订阅（改回固定深/浅色）。
        public static void FollowSystem(Window window, bool follow)
        {
            if (window == null) return;
            if (follow) SystemThemeWatcher.Watch(window, WindowBackdropType.None);
            else SystemThemeWatcher.UnWatch(window);
        }
    }
}
