using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using ImageViewer.Runtime;
using ImageViewer.Services;
using ImageViewer.Views;
using Microsoft.Win32;

namespace ImageViewer
{
    public partial class App : Application
    {
        private string[] startupArgs;
        private AppTheme theme;

        protected override void OnStartup(StartupEventArgs e)
        {
            startupArgs = e.Args;
            // 显式关停：首启提示窗/图片选择对话框关闭时不能让 WPF 以「最后窗口关闭」自动结束进程。
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var paths = ImageViewerPaths.ForCurrentUser();
            AppLogging.Initialize(paths.LogsDirectory, LogSeverity.Error);
            AppLogging.RegisterGlobalExceptionHandlers();
            ImageViewerPaths.MigrateLegacyConfig();
            AppLogging.Initialize(
                paths.LogsDirectory,
                AppLogging.ResolveMinimumLevel(AppSettingsFile.GetAll(ImageViewerPaths.ConfigFilePath)));

            // 应用外观主题（在任何窗口显示前）。
            theme = AppThemeStore.Load(ImageViewerPaths.ConfigFilePath);
            ThemeApplier.Apply(theme);

            // 提权子进程分支：以管理员身份完成 HKLM 注册/取消后立即退出，不显示任何界面。
            var elevatedAction = ElevatedCommand.Parse(startupArgs);
            if (elevatedAction != ElevatedAction.None)
            {
                Shutdown(RunAssociationCommand(elevatedAction));
                return;
            }

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

        // 关联命令分支：机器级注册在管理员上下文完成；打开方式候选必须在当前登录用户上下文写入。
        private static int RunAssociationCommand(ElevatedAction action)
        {
            try
            {
                var executablePath = Process.GetCurrentProcess().MainModule.FileName;
                if (action == ElevatedAction.Register)
                {
                    new FileAssociationService(Registry.LocalMachine).Register(executablePath);
                }
                else if (action == ElevatedAction.RegisterUserCandidates)
                {
                    new FileAssociationService().RegisterUserCandidates(executablePath);
                }
                else
                {
                    new FileAssociationService(Registry.LocalMachine).Unregister(executablePath);
                }

                Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "File association command completed: " + action, null);
                return 0;
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "File association command failed: " + action, error);
                return 1;
            }
        }

        // 命令行图片参数优先；无有效参数（含双击启动）时打开空白查看器，由右键菜单打开图片。
        private void ContinueStartup()
        {
            IReadOnlyList<string> imagePaths;
            if (CommandLineImageArgument.TryResolveAll(startupArgs, out imagePaths))
            {
                ShowViewer(imagePaths);
                return;
            }

            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动无有效图片参数，打开空白查看器", null);
            ShowViewer(null);
        }

        private void ShowViewer(IReadOnlyList<string> imagePaths)
        {
            Diagnostics.Sink.Log(LogSeverity.Warn, "ImageViewer", "启动模式判定：查看器，" + (imagePaths == null ? 0 : imagePaths.Count) + " 张", null);
            try
            {
                var viewer = new ViewerWindow(imagePaths);
                MainWindow = viewer;
                viewer.Show();
                // 跟随系统主题时订阅系统深浅色变化。
                ThemeApplier.FollowSystem(viewer, theme == AppTheme.System);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "打开看图窗口失败，应用退出", error);
                Shutdown(1);
            }
        }

        // 首启引导：询问是否设为默认图片查看器，按选择注册或持久化拒绝，然后继续启动。
        // async void 必须自兜异常：注册/打开系统设置失败不能中断启动流程。
        private async void PromptForDefaultViewerAsync(ViewerAssociationService associations)
        {
            try
            {
                Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "弹出首启默认查看器引导", null);
                var dialog = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "设为默认图片查看器",
                    Content = "将 ImageViewer 设为默认图片查看器？会先注册为打开候选，并清除旧的图片默认应用选择（下次打开 jpg/jpeg/png/bmp 会重新询问）；Windows 不允许程序自动设为默认，需在随后打开的系统设置里确认。",
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
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "首启默认查看器引导失败", error);
            }

            ContinueStartup();
        }
    }
}
