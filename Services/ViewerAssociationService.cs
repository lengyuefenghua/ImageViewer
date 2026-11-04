using System;
using System.ComponentModel;
using System.Diagnostics;
using ImageViewer.Runtime;

namespace ImageViewer.Services
{
    // 查看器的文件关联编排：用户级候选（HKCU）+ 机器级注册（管理员直写或提权子进程），并返回可直接显示的界面文案。
    public sealed class ViewerAssociationService
    {
        private const string LoggerName = "ImageViewer";
        private readonly FileAssociationService machineAssociations;
        private readonly FileAssociationService userAssociations;
        private readonly FileAssociationRegistrar registrar;
        private readonly Func<string> executablePath;
        private readonly string preferencesPath;
        private bool promptDismissed;

        public ViewerAssociationService(
            FileAssociationService machineAssociations,
            FileAssociationService userAssociations,
            FileAssociationRegistrar registrar = null,
            Func<string> executablePath = null,
            string preferencesPath = null)
        {
            if (machineAssociations == null) throw new ArgumentNullException("machineAssociations");
            if (userAssociations == null) throw new ArgumentNullException("userAssociations");
            this.machineAssociations = machineAssociations;
            this.userAssociations = userAssociations;
            this.registrar = registrar ?? new FileAssociationRegistrar();
            this.executablePath = executablePath ?? DefaultExecutablePath;
            this.preferencesPath = String.IsNullOrWhiteSpace(preferencesPath)
                ? ResolveDefaultPreferencesPath()
                : preferencesPath;
            promptDismissed = ViewerPreferencesStore.TryLoadDismissed(this.preferencesPath);
        }

        public bool IsRegistered { get { return machineAssociations.IsRegistered(executablePath()); } }

        // 任意一个图片扩展名的默认程序指向本程序即视为已是默认（与首启引导判定一致）。
        public bool IsDefaultViewer { get { return userAssociations.IsDefaultViewer(executablePath()); } }

        // 首启引导条件：用户未拒绝过、当前不是默认应用、且注册未指向当前 exe（程序移动后注册会过时）。
        public bool ShouldPromptForDefaultViewer()
        {
            if (promptDismissed) return false;
            if (userAssociations.IsDefaultViewer(executablePath())) return false;
            return !machineAssociations.IsRegisteredFor(executablePath());
        }

        public void DismissDefaultViewerPrompt()
        {
            promptDismissed = true;
            ViewerPreferencesStore.SaveDismissed(preferencesPath, true);
            Diagnostics.Sink.Log(LogSeverity.Info, LoggerName, "用户选择不再提示设为默认图片查看器：" + executablePath(), null);
        }

        public string Register()
        {
            var exe = executablePath();
            try
            {
                // 用户级候选（FileExts\OpenWithProgids）必须在用户上下文写入；提权子进程的 HKCU 是管理员账户。
                userAssociations.Unregister(exe);
                userAssociations.RegisterUserCandidates(exe);
                if (registrar.IsAdministrator)
                {
                    machineAssociations.Register(exe);
                }
                else
                {
                    var exitCode = registrar.RunElevatedRegister();
                    if (exitCode != 0) return "注册失败：管理员进程返回码 " + exitCode + "。";
                }

                if (!machineAssociations.IsRegistered(exe)) return "注册未完成。";

                // 用户明确要求设为默认，这里直接写入 UserChoice；被系统或安全软件拦截时降级为引导。
                var set = userAssociations.SetDefaultViewer(exe);
                if (set.Failed == 0)
                {
                    return "已注册，并已将 jpg/jpeg/png/bmp 的默认打开程序设为 ImageViewer。";
                }
                return "已注册为图片打开候选，但 " + set.Failed + " 项默认设置未生效（可能被系统或安全软件拦截）；请在系统「默认应用」里把 jpg/jpeg/png/bmp 选为 ImageViewer。";
            }
            catch (Win32Exception error) when (error.NativeErrorCode == 1223)
            {
                return "已取消管理员授权，未注册。";
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "注册图片文件关联失败：" + exe, error);
                return "注册失败：" + error.Message;
            }
        }

        public string Unregister()
        {
            var exe = executablePath();
            try
            {
                userAssociations.Unregister(exe);
                userAssociations.UnregisterUserCandidates(exe);
                if (registrar.IsAdministrator)
                {
                    machineAssociations.Unregister(exe);
                }
                else
                {
                    var exitCode = registrar.RunElevatedUnregister();
                    if (exitCode != 0) return "取消关联失败：管理员进程返回码 " + exitCode + "。";
                }

                return machineAssociations.IsRegistered(exe) ? "取消关联未完成。" : "已取消关联。";
            }
            catch (Win32Exception error) when (error.NativeErrorCode == 1223)
            {
                return "已取消管理员授权，未取消关联。";
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "取消图片文件关联失败：" + exe, error);
                return "取消关联失败：" + error.Message;
            }
        }

        public void OpenDefaultAppsSettings()
        {
            machineAssociations.OpenDefaultAppsSettings();
        }

        private static string DefaultExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        private static string ResolveDefaultPreferencesPath()
        {
            return ImageViewerPaths.ConfigFilePath;
        }
    }
}
