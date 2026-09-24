using System;
using System.Diagnostics;
using System.Security.Principal;
using ImageViewer.Core.Diagnostics;

namespace ImageViewer.App.Services
{
    // 机器级文件关联的提权执行：非管理员时以 runas 重启自身（命令行分支）完成 HKLM 注册。
    public sealed class FileAssociationRegistrar
    {
        private const string LoggerName = "ImageViewer";
        public const string RegisterArgument = "--register-file-associations";
        public const string UnregisterArgument = "--unregister-file-associations";
        private readonly Func<bool> isAdministrator;
        private readonly Func<string, int> runElevated;

        public FileAssociationRegistrar(Func<bool> isAdministrator = null, Func<string, int> runElevated = null)
        {
            this.isAdministrator = isAdministrator ?? DetectAdministrator;
            this.runElevated = runElevated ?? StartElevatedSelf;
        }

        public bool IsAdministrator { get { return isAdministrator(); } }

        public int RunElevatedRegister()
        {
            return runElevated(RegisterArgument);
        }

        public int RunElevatedUnregister()
        {
            return runElevated(UnregisterArgument);
        }

        private static bool DetectAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception error)
            {
                // 无法判定时按非管理员处理：走提权分支，由 UAC 决定。
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "检测管理员权限失败，按非管理员处理", error);
                return false;
            }
        }

        // 以管理员身份启动自身执行命令行分支；用户拒绝 UAC 时抛 Win32Exception(1223)，由调用方提示。
        private static int StartElevatedSelf(string argument)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = argument,
                UseShellExecute = true,
                Verb = "runas"
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null) return -1;
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}
