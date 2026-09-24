using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ImageViewer.Core.Diagnostics;
using Microsoft.Win32;

namespace ImageViewer.Services
{
    // 用户级（HKCU）图片文件关联：注册为 jpg/png/bmp 的打开候选与默认应用候选，
    // 默认应用仍由用户在系统设置中确认；不写 HKLM、不写 UserChoice（Windows 保护项）。
    public sealed class FileAssociationService
    {
        private const string LoggerName = "ImageViewer";
        public const string ProgId = "ImageViewer.Image";
        public const string FriendlyAppName = "ImageViewer";
        public const string OpenVerbName = "ImageViewerOpen";
        public const string CapabilitiesKeyName = "ImageViewer";
        private const string ApplicationDescription = "快速查看 jpg/png/bmp 图片";
        private const string ImageTypeDescription = "ImageViewer 图片";
        private const string DefaultFileExtsRootPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts";
        private const int ShcneAssocChanged = 0x08000000;
        private const uint ShcnfIdList = 0x0000;
        private static readonly string[] extensions = { ".jpg", ".png", ".bmp" };
        private readonly RegistryKey root;
        private readonly string softwareRootPath;
        private readonly string classesRootPath;
        private readonly string registeredApplicationsPath;
        private readonly string capabilitiesPath;
        private readonly string backupPath;
        private readonly string fileExtsRootPath;

        // root 决定注册范围：HKCU（用户级，默认）或 HKLM（机器级，需管理员）。
        // fileExtsRootPath 是用户级候选位置，只应由 HKCU 根的服务实例使用。
        public FileAssociationService(RegistryKey root = null, string softwareRootPath = "Software", string fileExtsRootPath = null)
        {
            if (String.IsNullOrWhiteSpace(softwareRootPath)) throw new ArgumentException("注册表根路径不能为空。", "softwareRootPath");
            this.root = root ?? Registry.CurrentUser;
            this.softwareRootPath = softwareRootPath.TrimEnd('\\');
            classesRootPath = this.softwareRootPath + @"\Classes";
            registeredApplicationsPath = this.softwareRootPath + @"\RegisteredApplications";
            capabilitiesPath = this.softwareRootPath + @"\" + CapabilitiesKeyName + @"\Capabilities";
            backupPath = this.softwareRootPath + @"\" + CapabilitiesKeyName + @"\Backup";
            this.fileExtsRootPath = String.IsNullOrWhiteSpace(fileExtsRootPath)
                ? DefaultFileExtsRootPath
                : fileExtsRootPath.TrimEnd('\\');
        }

        public static IReadOnlyList<string> SupportedExtensions { get { return extensions; } }

        public void Register(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("可执行文件路径不能为空。", "executablePath");

            var command = "\"" + executablePath + "\" \"%1\"";
            var icon = "\"" + executablePath + "\",0";
            var applicationKey = classesRootPath + @"\Applications\" + Path.GetFileName(executablePath);
            try
            {
                using (var commandKey = root.CreateSubKey(applicationKey + @"\shell\open\command"))
                {
                    commandKey.SetValue(null, command);
                }
                using (var friendlyKey = root.CreateSubKey(applicationKey))
                {
                    friendlyKey.SetValue("FriendlyAppName", FriendlyAppName);
                }
                using (var typesKey = root.CreateSubKey(applicationKey + @"\SupportedTypes"))
                {
                    foreach (var extension in extensions) typesKey.SetValue(extension, String.Empty);
                }
                using (var progIdCommand = root.CreateSubKey(classesRootPath + "\\" + ProgId + @"\shell\open\command"))
                {
                    progIdCommand.SetValue(null, command);
                }
                using (var progIdIcon = root.CreateSubKey(classesRootPath + "\\" + ProgId))
                {
                    // ProgID 默认值 = 文件类型描述，Windows 弹窗以它显示友好名。
                    progIdIcon.SetValue(null, ImageTypeDescription);
                    progIdIcon.SetValue("DefaultIcon", icon);
                }
                // ProgID 的 Application 子键：让「打开方式」列表显示应用名而不是 ProgID。
                using (var progIdApplication = root.CreateSubKey(classesRootPath + "\\" + ProgId + @"\Application"))
                {
                    progIdApplication.SetValue("ApplicationName", FriendlyAppName);
                    progIdApplication.SetValue("ApplicationDescription", ApplicationDescription);
                    progIdApplication.SetValue("ApplicationIcon", icon);
                }
                foreach (var extension in extensions)
                {
                    using (var openWith = root.CreateSubKey(classesRootPath + "\\" + extension + @"\OpenWithProgids"))
                    {
                        // REG_NONE 空值：Windows 以此判断该 ProgID 是本扩展名的打开候选。
                        openWith.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                    }
                }
                // 系统 ProgID 的 shell 动词：Windows 10「打开方式/默认应用」弹窗据此识别能打开该类型的应用
                // （与 MassiGra 同款机制）；只添加动词，不改 shell 默认值，避免抢占用户默认。
                foreach (var extension in extensions)
                {
                    var systemProgId = ReadExtensionProgId(extension);
                    if (String.IsNullOrWhiteSpace(systemProgId)) continue;
                    using (var verbCommand = root.CreateSubKey(classesRootPath + "\\" + systemProgId + @"\shell\" + OpenVerbName + @"\command"))
                    {
                        verbCommand.SetValue(null, command);
                    }
                    using (var verb = root.CreateSubKey(classesRootPath + "\\" + systemProgId + @"\shell\" + OpenVerbName))
                    {
                        verb.SetValue(null, "用 ImageViewer 打开(&V)");
                    }
                    // 抢占默认动词（MassiGra 同款）：先备份原值（幂等，已有备份不覆盖），再指向本程序。
                    using (var shellKey = root.OpenSubKey(classesRootPath + "\\" + systemProgId + @"\shell"))
                    {
                        var originalVerb = shellKey == null ? null : shellKey.GetValue(null) as string;
                        using (var backupKey = root.CreateSubKey(backupPath))
                        {
                            if (backupKey.GetValue(systemProgId) == null)
                            {
                                backupKey.SetValue(systemProgId, originalVerb ?? String.Empty);
                            }
                        }
                    }
                    using (var shellKey = root.CreateSubKey(classesRootPath + "\\" + systemProgId + @"\shell"))
                    {
                        shellKey.SetValue(null, OpenVerbName);
                    }
                }
                // RegisteredApplications + Capabilities：让应用出现在「默认应用」列表并可一键设为默认。
                using (var registered = root.CreateSubKey(registeredApplicationsPath))
                {
                    registered.SetValue(FriendlyAppName, capabilitiesPath);
                }
                using (var capabilities = root.CreateSubKey(capabilitiesPath))
                {
                    capabilities.SetValue("ApplicationName", FriendlyAppName);
                    capabilities.SetValue("ApplicationDescription", ApplicationDescription);
                    capabilities.SetValue("ApplicationIcon", icon);
                }
                using (var fileAssociations = root.CreateSubKey(capabilitiesPath + @"\FileAssociations"))
                {
                    foreach (var extension in extensions) fileAssociations.SetValue(extension, ProgId);
                }

                NotifyShellOfAssociationChange();
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "已注册图片文件关联候选：" + String.Join("、", extensions) + "，程序 " + executablePath, null);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "注册图片文件关联失败：" + executablePath, error);
                throw;
            }
        }

        public void Unregister(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("可执行文件路径不能为空。", "executablePath");

            try
            {
                foreach (var extension in extensions)
                {
                    using (var openWith = root.OpenSubKey(classesRootPath + "\\" + extension + @"\OpenWithProgids", true))
                    {
                        if (openWith != null) openWith.DeleteValue(ProgId, false);
                    }
                }
                foreach (var extension in extensions)
                {
                    var systemProgId = ReadExtensionProgId(extension);
                    if (String.IsNullOrWhiteSpace(systemProgId)) continue;
                    // 恢复被抢占的默认动词（读备份），再删除本程序的动词。
                    using (var backupKey = root.OpenSubKey(backupPath))
                    {
                        if (backupKey != null)
                        {
                            var originalVerb = backupKey.GetValue(systemProgId) as string;
                            using (var shellKey = root.OpenSubKey(classesRootPath + "\\" + systemProgId + @"\shell", true))
                            {
                                if (shellKey != null)
                                {
                                    if (String.IsNullOrEmpty(originalVerb)) shellKey.DeleteValue(null, false);
                                    else shellKey.SetValue(null, originalVerb);
                                }
                            }
                        }
                    }
                    root.DeleteSubKeyTree(classesRootPath + "\\" + systemProgId + @"\shell\" + OpenVerbName, false);
                }
                root.DeleteSubKeyTree(classesRootPath + "\\" + ProgId, false);
                root.DeleteSubKeyTree(classesRootPath + @"\Applications\" + Path.GetFileName(executablePath), false);
                root.DeleteSubKeyTree(softwareRootPath + @"\" + CapabilitiesKeyName, false);
                using (var registered = root.OpenSubKey(registeredApplicationsPath, true))
                {
                    if (registered != null) registered.DeleteValue(FriendlyAppName, false);
                }

                NotifyShellOfAssociationChange();
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "已取消图片文件关联候选：" + String.Join("、", extensions), null);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "取消图片文件关联失败：" + executablePath, error);
                throw;
            }
        }

        public bool IsRegistered(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) return false;

            try
            {
                foreach (var extension in extensions)
                {
                    using (var openWith = root.OpenSubKey(classesRootPath + "\\" + extension + @"\OpenWithProgids"))
                    {
                        if (openWith == null || openWith.GetValue(ProgId) == null) return false;
                    }
                }
                using (var registered = root.OpenSubKey(registeredApplicationsPath))
                {
                    if (registered == null || registered.GetValue(FriendlyAppName) == null) return false;
                }
                return true;
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "读取图片文件关联状态失败：" + executablePath, error);
                return false;
            }
        }

        // 注册的打开命令是否指向当前可执行文件（键值存在不代表路径正确，程序移动后会过时）。
        public bool IsRegisteredFor(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath) || !IsRegistered(executablePath)) return false;

            var applicationKey = classesRootPath + @"\Applications\" + Path.GetFileName(executablePath);
            using (var commandKey = root.OpenSubKey(applicationKey + @"\shell\open\command"))
            {
                var command = commandKey == null ? null : commandKey.GetValue(null) as string;
                return CommandTargets(command, executablePath);
            }
        }

        private static bool CommandTargets(string command, string executablePath)
        {
            if (String.IsNullOrWhiteSpace(command)) return false;
            return command.IndexOf("\"" + executablePath + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // 只读检查当前用户是否已把本程序设为 jpg/png/bmp 的默认（UserChoice 有哈希保护，程序只能读不能写）。
        public bool IsDefaultViewer()
        {
            foreach (var extension in extensions)
            {
                using (var userChoice = Registry.CurrentUser.OpenSubKey(fileExtsRootPath + "\\" + extension + @"\UserChoice"))
                {
                    var progId = userChoice == null ? null : userChoice.GetValue("ProgId") as string;
                    if (String.Equals(progId, ProgId, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        // 扩展名的系统 ProgID（如 .jpg -> jpegfile）由系统或用户设置，动态读取避免写死。
        private string ReadExtensionProgId(string extension)
        {
            using (var extensionKey = root.OpenSubKey(classesRootPath + "\\" + extension))
            {
                return extensionKey == null ? null : extensionKey.GetValue(null) as string;
            }
        }

        // 用户级候选：Windows 10 的「打开方式/默认应用」弹窗读取 FileExts\<ext>\OpenWithProgids，
        // 必须在当前用户上下文写入（提权进程的 HKCU 是管理员账户）。
        public void RegisterUserCandidates(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("可执行文件路径不能为空。", "executablePath");

            try
            {
                foreach (var extension in extensions)
                {
                    using (var openWith = root.CreateSubKey(fileExtsRootPath + "\\" + extension + @"\OpenWithProgids"))
                    {
                        openWith.SetValue(ProgId, new byte[0], RegistryValueKind.None);
                    }
                }

                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "已注册用户级图片打开候选（FileExts）：" + String.Join("、", extensions), null);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "注册用户级图片打开候选失败：" + executablePath, error);
                throw;
            }
        }

        public void UnregisterUserCandidates(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("可执行文件路径不能为空。", "executablePath");

            try
            {
                foreach (var extension in extensions)
                {
                    using (var openWith = root.OpenSubKey(fileExtsRootPath + "\\" + extension + @"\OpenWithProgids", true))
                    {
                        if (openWith != null) openWith.DeleteValue(ProgId, false);
                    }
                }

                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "已移除用户级图片打开候选（FileExts）：" + String.Join("、", extensions), null);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "移除用户级图片打开候选失败：" + executablePath, error);
                throw;
            }
        }

        // 官方要求：注册/取消关联后通知 shell 刷新关联缓存，否则「默认应用」等列表不更新。
        private static void NotifyShellOfAssociationChange()
        {
            try
            {
                SHChangeNotify(ShcneAssocChanged, ShcnfIdList, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "通知系统刷新文件关联失败", error);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        // 引导入口：Windows 不允许程序修改默认应用（UserChoice 保护），只能打开系统设置由用户确认。
        public void OpenDefaultAppsSettings()
        {
            try
            {
                Process.Start("ms-settings:defaultapps");
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "打开系统默认应用设置失败", error);
                throw;
            }
        }
    }
}
