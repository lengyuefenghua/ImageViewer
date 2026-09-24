using System;
using System.Windows;
using ImageViewer.App.Runtime;
using ImageViewer.App.Standalone;
using ImageViewer.Core.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace ImageViewer.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var paths = ImageViewerPaths.ForCurrentUser();
            AppLogging.Initialize(paths.LogsDirectory, LogSeverity.Error);
            AppLogging.RegisterGlobalExceptionHandlers();

            // 命令行图片参数优先；无有效参数（含双击启动）时弹出图片选择对话框。
            string imagePath;
            if (CommandLineImageArgument.TryResolve(e.Args, out imagePath))
            {
                ShowViewer(imagePath);
                return;
            }

            PickImageAndView();
        }

        private void ShowViewer(string imagePath)
        {
            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动模式判定：独立查看器，" + imagePath, null);
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            var viewer = new StandaloneViewerWindow(imagePath);
            MainWindow = viewer;
            viewer.Show();
        }

        // 无有效图片参数（含双击 exe）：弹图片选择对话框，选中即看图、取消则退出，不打开空白窗口。
        private void PickImageAndView()
        {
            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动无有效图片参数，打开图片选择对话框", null);
            var dialog = new OpenFileDialog
            {
                Title = "选择图片",
                Filter = "图片 (*.jpg;*.png;*.bmp)|*.jpg;*.png;*.bmp|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() == true && !String.IsNullOrWhiteSpace(dialog.FileName))
            {
                ShowViewer(dialog.FileName);
                return;
            }

            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "未选择图片，退出", null);
            Shutdown();
        }
    }
}
