using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ImageViewer.Configuration;
using ImageViewer.Runtime;
using ImageViewer.Services;
using ImageViewer.Standalone;
using ImageViewer.Core.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace ImageViewer.Views
{
    // 设置窗口：文件关联的注册 / 取消 / 默认应用引导，以及最低日志级别配置。
    public partial class SettingsWindow : FluentWindow
    {
        private readonly ViewerAssociationService associations;
        private bool initializingLogLevel;
        private static readonly AppTheme[] ThemeValues = { AppTheme.Dark, AppTheme.Light, AppTheme.System };
        private static readonly string[] ThemeDisplays = { "深色", "浅色", "跟随系统" };
        private bool initializingTheme;

        public SettingsWindow()
        {
            InitializeComponent();
            // 机器级写 HKLM，用户级写 HKCU；两者都指向本进程 exe。
            associations = new ViewerAssociationService(
                new FileAssociationService(Registry.LocalMachine),
                new FileAssociationService());
            Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "打开设置窗口", null);
            InitializeLogLevel();
            InitializeTheme();
            AboutVersionText.Text = "版本：" + AppVersion;
            CopyShortcutBox.Text = CopySettingsStore.Load(ImageViewerPaths.ConfigFilePath).Shortcut;
            UpdateStatus();
        }

        // 版本取自程序集（csproj <Version>），避免界面与工程版本不一致。
        private static string AppVersion
        {
            get
            {
                var version = typeof(SettingsWindow).Assembly.GetName().Version;
                return version == null ? "1.0.0" : version.ToString(3);
            }
        }

        // 下拉项：深色/浅色/跟随系统；初值取配置文件。
        private void InitializeTheme()
        {
            initializingTheme = true;
            foreach (var display in ThemeDisplays) ThemeComboBox.Items.Add(display);
            var index = Array.IndexOf(ThemeValues, AppThemeStore.Load(ImageViewerPaths.ConfigFilePath));
            ThemeComboBox.SelectedIndex = index >= 0 ? index : 0;
            initializingTheme = false;
        }

        private void ThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (initializingTheme) return;
            var index = ThemeComboBox.SelectedIndex;
            if (index < 0 || index >= ThemeValues.Length) return;

            var theme = ThemeValues[index];
            ThemeApplier.Apply(theme);
            // 「跟随系统」订阅系统主题变化；固定深/浅色时取消订阅。
            ThemeApplier.FollowSystem(Owner, theme == AppTheme.System);
            try
            {
                AppThemeStore.Save(ImageViewerPaths.ConfigFilePath, theme);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "保存主题设置失败", error);
            }
        }

        // 按键录制：按下的键即复制快捷键并立即持久化（忽略纯修饰键）。
        private void CopyShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl
                || e.Key == Key.LeftShift || e.Key == Key.RightShift
                || e.Key == Key.LeftAlt || e.Key == Key.RightAlt
                || e.Key == Key.System || e.Key == Key.None)
            {
                return;
            }

            var key = e.Key.ToString();
            CopyShortcutBox.Text = key;
            try
            {
                var settings = CopySettingsStore.Load(ImageViewerPaths.ConfigFilePath);
                settings.Shortcut = key;
                CopySettingsStore.Save(ImageViewerPaths.ConfigFilePath, settings);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "保存复制快捷键失败", error);
            }
        }

        // 下拉项用 LogSeverity 枚举值填充，显示名即枚举名；初值取配置文件。
        private void InitializeLogLevel()
        {
            initializingLogLevel = true;
            foreach (LogSeverity level in Enum.GetValues(typeof(LogSeverity)))
            {
                LogLevelComboBox.Items.Add(level);
            }
            LogLevelComboBox.SelectedItem = AppLogging.ResolveMinimumLevel(AppSettingsFile.GetAll(ImageViewerPaths.ConfigFilePath));
            initializingLogLevel = false;
        }

        private void LogLevelSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 初始化填充时的选中不算用户切换。
            if (initializingLogLevel || LogLevelComboBox.SelectedItem == null) return;

            var level = (LogSeverity)LogLevelComboBox.SelectedItem;
            AppLogging.SetMinimumLevel(level);
            try
            {
                AppSettingsFile.Set(ImageViewerPaths.ConfigFilePath, new Dictionary<string, string>
                {
                    { AppLogging.MinimumLevelKey, level.ToString() }
                });
            }
            catch (Exception error)
            {
                // 持久化失败不影响本次会话已生效的级别。
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "保存日志级别失败", error);
            }
        }

        private void RegisterClick(object sender, RoutedEventArgs e)
        {
            StatusText.Text = associations.Register();
            // 注册成功后自动打开系统「默认应用」页，省去用户手动查找；成为默认仍需 Windows 由用户确认。
            if (associations.IsRegistered) OpenDefaultAppsSettings();
        }

        private void UnregisterClick(object sender, RoutedEventArgs e)
        {
            StatusText.Text = associations.Unregister();
        }

        private void OpenDefaultAppsClick(object sender, RoutedEventArgs e)
        {
            OpenDefaultAppsSettings();
        }

        private void OpenDefaultAppsSettings()
        {
            try
            {
                associations.OpenDefaultAppsSettings();
            }
            catch (Exception error)
            {
                StatusText.Text = "打开系统默认应用设置失败：" + error.Message;
            }
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateStatus()
        {
            StatusText.Text = associations.IsRegistered
                ? "当前已注册为图片打开候选。"
                : "当前未注册。";
        }
    }
}
