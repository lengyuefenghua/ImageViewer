using System;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ImageViewer.Runtime
{
    // 把 AppTheme 应用到 Wpf.Ui 主题系统；System 时订阅系统深浅色变化实时跟随。
    public static class ThemeApplier
    {
        private static bool backgroundRefreshHooked;

        public static void Apply(AppTheme theme)
        {
            HookBackgroundRefresh();
            if (theme == AppTheme.Light) ApplicationThemeManager.Apply(ApplicationTheme.Light);
            else if (theme == AppTheme.System) ApplicationThemeManager.ApplySystemTheme();
            else ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        }

        // ThemeApplier 只在启动与设置窗主题切换时调用，首次进入完成一次性订阅即可。
        private static void HookBackgroundRefresh()
        {
            if (backgroundRefreshHooked) return;
            backgroundRefreshHooked = true;
            ApplicationThemeManager.Changed += (theme, _) => RefreshOpenWindowBackgrounds(theme);
        }

        // Wpf.Ui 的 ApplicationThemeManager.Apply 只重建 MainWindow 的背景；其余已打开窗口的背景
        // 在创建时被 WindowBackdrop.RemoveBackdrop 冻结成固定画刷，必须按新主题显式重建，
        // 否则会留下旧背景（如深色主题下设置窗仍是浅底浅字）。
        private static void RefreshOpenWindowBackgrounds(ApplicationTheme theme)
        {
            var application = Application.Current;
            if (application == null) return;

            var refreshed = 0;
            foreach (Window window in application.Windows)
            {
                if (!window.IsLoaded || window == application.MainWindow) continue;
                var backdrop = window is FluentWindow fluent ? fluent.WindowBackdropType : WindowBackdropType.None;
                WindowBackgroundManager.UpdateBackground(window, theme, backdrop);
                refreshed++;
            }

            Diagnostics.Sink.Log(
                LogSeverity.Debug,
                "ImageViewer",
                "主题切换后重建非主窗口背景：" + theme + "，窗口数 " + refreshed,
                null);
        }

        // follow=true 时跟随系统主题变化；false 时取消订阅（改回固定深/浅色）。
        public static void FollowSystem(Window window, bool follow)
        {
            if (window == null) return;
            if (follow)
            {
                SystemThemeWatcher.Watch(window, WindowBackdropType.None);
                return;
            }

            // 关窗阶段句柄可能已销毁，UnWatch 会抛 InvalidOperationException；忽略即可。
            try
            {
                SystemThemeWatcher.UnWatch(window);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
