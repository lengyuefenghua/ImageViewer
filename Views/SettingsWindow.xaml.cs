using System;
using System.Windows;
using ImageViewer.Services;
using ImageViewer.Core.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace ImageViewer.Views
{
    // 设置窗口：文件关联的注册 / 取消 / 默认应用引导入口。
    public partial class SettingsWindow : FluentWindow
    {
        private readonly ViewerAssociationService associations;

        public SettingsWindow()
        {
            InitializeComponent();
            // 机器级写 HKLM，用户级写 HKCU；两者都指向本进程 exe。
            associations = new ViewerAssociationService(
                new FileAssociationService(Registry.LocalMachine),
                new FileAssociationService());
            Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "打开设置窗口", null);
            UpdateStatus();
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
