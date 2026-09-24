using System;
using System.Windows;
using ImageViewer.App.Runtime;
using ImageViewer.App.Standalone;
using ImageViewer.Core.Diagnostics;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ImageViewer.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 命令行图片参数优先：有效则进入查看器；否则提示用法并退出，不打开空白看图窗口。
            string imagePath;
            if (CommandLineImageArgument.TryResolve(e.Args, out imagePath))
            {
                StartViewer(imagePath);
                return;
            }

            ShowUsageAndExit();
        }

        private void StartViewer(string imagePath)
        {
            var paths = ImageViewerPaths.ForCurrentUser();
            AppLogging.Initialize(paths.LogsDirectory, LogSeverity.Error);
            AppLogging.RegisterGlobalExceptionHandlers();
            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动模式判定：独立查看器，" + imagePath, null);
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            var viewer = new StandaloneViewerWindow(imagePath);
            MainWindow = viewer;
            viewer.Show();
        }

        // 无有效图片参数：显示简短用法提示，用户确认后以退出码 2 结束进程。
        private void ShowUsageAndExit()
        {
            var paths = ImageViewerPaths.ForCurrentUser();
            AppLogging.Initialize(paths.LogsDirectory, LogSeverity.Error);
            AppLogging.RegisterGlobalExceptionHandlers();
            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动缺少有效图片参数，显示用法提示后退出", null);
            // 显式退出：避免关闭提示窗时 WPF 以默认退出码 0 自动结束，确保以退出码 2 表示用法错误。
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var dialog = new Wpf.Ui.Controls.MessageBox
            {
                Title = "ImageViewer",
                Content = "请通过命令行传入单个 jpg/png/bmp 图片路径，例如：\nImageViewer.exe \"D:\\photos\\a.jpg\"",
                PrimaryButtonText = "退出"
            };
            dialog.ShowDialogAsync().ContinueWith(_ => Dispatcher.Invoke(new Action(() => Shutdown(2))));
        }
    }
}
