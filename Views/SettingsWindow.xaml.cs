using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ImageViewer.Configuration;
using ImageViewer.Runtime;
using ImageViewer.Services;
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

        public SettingsWindow()
        {
            InitializeComponent();
            // 机器级写 HKLM，用户级写 HKCU；两者都指向本进程 exe。
            associations = new ViewerAssociationService(
                new FileAssociationService(Registry.LocalMachine),
                new FileAssociationService());
            Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "打开设置窗口", null);
            InitializeLogLevel();
            UpdateStatus();
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
        }

        private void UnregisterClick(object sender, RoutedEventArgs e)
        {
            StatusText.Text = associations.Unregister();
        }

        private void OpenDefaultAppsClick(object sender, RoutedEventArgs e)
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
