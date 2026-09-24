using System;
using System.Threading.Tasks;
using System.Windows;
using ImageViewer.Runtime;
using ImageViewer.Services;
using ImageViewer.Standalone;
using ImageViewer.Core.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace ImageViewer
{
    public partial class App : Application
    {
        private string[] startupArgs;

        protected override void OnStartup(StartupEventArgs e)
        {
            startupArgs = e.Args;
            // 显式关停：首启提示窗/图片选择对话框关闭时不能让 WPF 以「最后窗口关闭」自动结束进程。
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var paths = ImageViewerPaths.ForCurrentUser();
            AppLogging.Initialize(paths.LogsDirectory, LogSeverity.Error);
            AppLogging.RegisterGlobalExceptionHandlers();

            // 首启引导：任何启动方式（双击 exe、打开图片）都先询问是否设为默认查看器，答完再继续。
            var associations = new ViewerAssociationService(
                new FileAssociationService(Registry.LocalMachine),
                new FileAssociationService());
            if (associations.ShouldPromptForDefaultViewer())
            {
                PromptForDefaultViewerAsync(associations);
                return;
            }

            ContinueStartup();
        }

        // 命令行图片参数优先；无有效参数（含双击启动）时打开空白查看器，由右键菜单打开图片。
        private void ContinueStartup()
        {
            string imagePath;
            if (CommandLineImageArgument.TryResolve(startupArgs, out imagePath))
            {
                ShowViewer(imagePath);
                return;
            }

            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动无有效图片参数，打开空白查看器", null);
            ShowViewer(null);
        }

        private void ShowViewer(string imagePath)
        {
            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动模式判定：独立查看器，" + imagePath, null);
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            var viewer = new StandaloneViewerWindow(imagePath);
            MainWindow = viewer;
            viewer.Show();
        }

        // 首启引导：询问是否设为默认图片查看器，按选择注册或持久化拒绝，然后继续启动。
        private async void PromptForDefaultViewerAsync(ViewerAssociationService associations)
        {
            Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "弹出首启默认查看器引导", null);
            var dialog = new Wpf.Ui.Controls.MessageBox
            {
                Title = "设为默认图片查看器",
                Content = "将 ImageViewer 设为默认图片查看器？Windows 不允许程序自动设为默认，需在随后打开的系统设置里确认。",
                PrimaryButtonText = "设为默认",
                SecondaryButtonText = "以后再说"
            };
            var result = await dialog.ShowDialogAsync();
            if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "用户选择设为默认图片查看器", null);
                associations.Register();
                associations.OpenDefaultAppsSettings();
            }
            else if (result == Wpf.Ui.Controls.MessageBoxResult.Secondary)
            {
                associations.DismissDefaultViewerPrompt();
            }

            ContinueStartup();
        }
    }
}
