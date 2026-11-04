using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ImageViewer.Runtime;
using Microsoft.Win32;

namespace ImageViewer.Services
{
    // 图片文件关联：注册为 jpg/jpeg/png/bmp 的打开候选（Applications + OpenWithProgids + RegisteredApplications），
    // 并清理历史版本遗留的系统默认动词劫持。用户明确要求设为默认时，另由 SetDefaultViewer 写入带 Hash 的 UserChoice
    //（Hash 算法未公开，见 UserChoiceHash；Windows 更新后可能失效，调用方需准备降级）。
    public sealed class FileAssociationService
    {
        private const string LoggerName = "ImageViewer";
        public const string ProgId = "ImageViewer.Image";
        public const string FriendlyAppName = "ImageViewer";
        public const string OpenVerbName = "ImageViewerOpen";
        public const string CapabilitiesKeyName = "ImageViewer";
        private const string ApplicationDescription = "快速查看 jpg/jpeg/png/bmp 图片";
        private const string ImageTypeDescription = "ImageViewer 图片";
        private const string DefaultFileExtsRootPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts";
        private const int ShcneAssocChanged = 0x08000000;
        private const uint ShcnfIdList = 0x0000;
        private static readonly string[] extensions = { ".jpg", ".jpe", ".jpeg", ".jfif", ".png", ".bmp" };
        private readonly RegistryKey root;
        private readonly string softwareRootPath;
        private readonly string classesRootPath;
        private readonly string registeredApplicationsPath;
        private readonly string capabilitiesPath;
        private readonly string backupPath;
        private readonly string fileExtsRootPath;
        // 32 位视图的机器根（WOW6432Node）；null 且 root 非 HKLM 时跳过旧 32 位残留清理。
        private readonly RegistryKey legacy32Root;

        // root 决定注册范围：HKCU（用户级，默认）或 HKLM（机器级，需管理员）。
        // fileExtsRootPath 是用户级候选位置，只应由 HKCU 根的服务实例使用。
        // legacy32Root 是测试注入的 32 位视图根；生产环境缺省按 HKLM 自动解析。
        public FileAssociationService(RegistryKey root = null, string softwareRootPath = "Software", string fileExtsRootPath = null, RegistryKey legacy32Root = null)
        {
            if (String.IsNullOrWhiteSpace(softwareRootPath)) throw new ArgumentException("注册表根路径不能为空。", "softwareRootPath");
            this.root = root ?? Registry.CurrentUser;
            this.softwareRootPath = softwareRootPath.TrimEnd('\\');
            classesRootPath = this.softwareRootPath + @"\Classes";
            registeredApplicationsPath = this.softwareRootPath + @"\RegisteredApplications";
            capabilitiesPath = this.softwareRootPath + @"\" + CapabilitiesKeyName + @"\Capabilities";
            backupPath = this.softwareRootPath + @"\" + CapabilitiesKeyName + @"\Backup";
            this.legacy32Root = legacy32Root;
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
                // 历史版本曾把系统 ProgID 的默认动词指向本程序；注册时幂等清理，避免残留继续影响双击行为。
                foreach (var extension in extensions)
                {
                    var systemProgId = ReadExtensionProgId(extension);
                    if (!String.IsNullOrWhiteSpace(systemProgId)) RestoreHijackedDefaultVerb(systemProgId);
                }
                CleanupLegacy32BitRegistration();
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
                    if (!String.IsNullOrWhiteSpace(systemProgId)) RestoreHijackedDefaultVerb(systemProgId);
                }
                CleanupLegacy32BitRegistration();
                root.DeleteSubKeyTree(classesRootPath + "\\" + ProgId, false);
                root.DeleteSubKeyTree(classesRootPath + @"\Applications\" + Path.GetFileName(executablePath), false);
                root.DeleteSubKeyTree(softwareRootPath + @"\" + CapabilitiesKeyName, false);
                using (var registered = root.OpenSubKey(registeredApplicationsPath, true))
                {
                    if (registered != null) registered.DeleteValue(FriendlyAppName, false);
                }
                RemoveDefaultViewer(executablePath);

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

        // 只读检查当前用户是否已把本程序设为 jpg/jpeg/png/bmp 的默认（UserChoice 有哈希保护，程序只能读不能写）。
        // 用户经「打开方式」选定时系统写入的 ProgId 可能是 Applications\<exe> 形式，两种都算命中。
        public bool IsDefaultViewer(string executablePath = null)
        {
            if (String.IsNullOrWhiteSpace(executablePath))
            {
                executablePath = Process.GetCurrentProcess().MainModule.FileName;
            }
            var applicationProgId = ApplicationProgId(executablePath);
            foreach (var extension in extensions)
            {
                using (var userChoice = Registry.CurrentUser.OpenSubKey(fileExtsRootPath + "\\" + extension + @"\UserChoice"))
                {
                    var progId = userChoice == null ? null : userChoice.GetValue("ProgId") as string;
                    if (IsOurProgId(progId, applicationProgId)) return true;
                }
            }
            return false;
        }

        // 用户经「打开方式」选定本程序时，Windows 写入 UserChoice 的 ProgId 可能是 Applications\<exe 文件名>。
        private static string ApplicationProgId(string executablePath)
        {
            return @"Applications\" + Path.GetFileName(executablePath);
        }

        private static bool IsOurProgId(string progId, string applicationProgId)
        {
            return String.Equals(progId, ProgId, StringComparison.OrdinalIgnoreCase)
                || String.Equals(progId, applicationProgId, StringComparison.OrdinalIgnoreCase);
        }

        // 扩展名的系统 ProgID（如 .jpg -> jpegfile）由系统或用户设置，动态读取避免写死。
        private string ReadExtensionProgId(string extension)
        {
            using (var extensionKey = root.OpenSubKey(classesRootPath + "\\" + extension))
            {
                return extensionKey == null ? null : extensionKey.GetValue(null) as string;
            }
        }

        private string ReadBackedUpVerb(string systemProgId)
        {
            using (var backupKey = root.OpenSubKey(backupPath))
            {
                return backupKey == null ? null : backupKey.GetValue(systemProgId) as string;
            }
        }

        // 动词的 open 命令是否指向一个存在的本地程序；非绝对路径（如 rundll32.exe）无法判断，保守视为有效。
        private bool VerbTargetsExistingProgram(string systemProgId, string verbName)
        {
            using (var commandKey = root.OpenSubKey(classesRootPath + "\\" + systemProgId + @"\shell\" + verbName + @"\command"))
            {
                var command = commandKey == null ? null : commandKey.GetValue(null) as string;
                return CommandTargetsExistingProgram(command);
            }
        }

        private static bool CommandTargetsExistingProgram(string command)
        {
            if (String.IsNullOrWhiteSpace(command)) return true;
            var trimmed = command.Trim();
            string executable;
            if (trimmed.StartsWith("\"", StringComparison.Ordinal))
            {
                var end = trimmed.IndexOf('"', 1);
                if (end <= 1) return true;
                executable = trimmed.Substring(1, end - 1);
            }
            else
            {
                var space = trimmed.IndexOf(' ');
                executable = space > 0 ? trimmed.Substring(0, space) : trimmed;
            }
            // 只对绝对路径判存在性；相对/内置命令保守视为有效，避免误清。
            if (!Path.IsPathRooted(executable)) return true;
            return File.Exists(executable);
        }

        // 早期 32 位版本把机器级注册写进 HKLM 的 32 位视图（WOW6432Node），64 位进程不可见；注册/取消时一并清理。
        // Software\Classes 的文件关联键（ProgID、Applications\<exe>、扩展名 OpenWithProgids）与 Software\RegisteredApplications
        // 在 32/64 位视图间共享，从 32 位视图删除等于删掉当前注册，因此只清理真正被重定向的 Software\ImageViewer。
        private void CleanupLegacy32BitRegistration()
        {
            var root32 = ResolveLegacy32Root();
            if (root32 == null) return;

            try
            {
                if (DeleteSubKeyIfExists(root32, @"Software\" + CapabilitiesKeyName))
                {
                    Diagnostics.Sink.Log(LogSeverity.Info, LoggerName, "已清理 32 位视图的旧注册残留", null);
                }
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "清理 32 位视图的旧注册残留失败", error);
            }
        }

        // 32 位视图根：优先使用注入的测试根；生产环境仅在机器级（HKLM）root 上自动解析，用户级服务跳过。
        private RegistryKey ResolveLegacy32Root()
        {
            if (legacy32Root != null) return legacy32Root;
            return root.Name.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)
                ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                : null;
        }

        private static bool DeleteSubKeyIfExists(RegistryKey parent, string subKey)
        {
            using (var key = parent.OpenSubKey(subKey))
            {
                if (key == null) return false;
            }
            parent.DeleteSubKeyTree(subKey, false);
            return true;
        }

        // 历史版本写入的劫持：系统 ProgID 的默认动词指向本程序。默认动词仍指向本程序时还原备份，
        // 备份失效或不存在则清除默认值；无论默认是否指向本程序，都删除本程序自己的动词键。
        private void RestoreHijackedDefaultVerb(string systemProgId)
        {
            using (var shellKey = root.OpenSubKey(classesRootPath + "\\" + systemProgId + @"\shell", true))
            {
                if (shellKey != null
                    && String.Equals(shellKey.GetValue(null) as string, OpenVerbName, StringComparison.OrdinalIgnoreCase))
                {
                    var originalVerb = ReadBackedUpVerb(systemProgId);
                    if (!String.IsNullOrEmpty(originalVerb) && VerbTargetsExistingProgram(systemProgId, originalVerb))
                    {
                        shellKey.SetValue(null, originalVerb);
                    }
                    else
                    {
                        shellKey.DeleteValue(null, false);
                    }
                    Diagnostics.Sink.Log(LogSeverity.Info, LoggerName, "已修复历史版本的系统默认动词残留：" + systemProgId, null);
                }
            }
            root.DeleteSubKeyTree(classesRootPath + "\\" + systemProgId + @"\shell\" + OpenVerbName, false);
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

        // 清除用户对图片扩展名的旧默认选择（含系统保护的 UserChoice），让下次打开重新询问。
        // 用户已选定本程序时保留；单项失败不阻断其余项。
        public UserChoiceResetResult ResetStaleUserChoices(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("可执行文件路径不能为空。", "executablePath");

            var applicationProgId = ApplicationProgId(executablePath);
            var cleared = 0;
            var failed = 0;
            foreach (var extension in extensions)
            {
                try
                {
                    using (var key = root.OpenSubKey(fileExtsRootPath + "\\" + extension, true))
                    {
                        if (key == null) continue;
                        // Application/Progid 是旧式覆盖值，留着会让系统继续沿用先前的打开方式。
                        key.DeleteValue("Application", false);
                        key.DeleteValue("Progid", false);
                        var current = ReadUserChoiceProgId(key);
                        if (current == null || IsOurProgId(current, applicationProgId)) continue;
                        key.DeleteSubKeyTree("UserChoice", false);
                        cleared++;
                    }
                }
                catch (Exception error)
                {
                    failed++;
                    Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "重置旧默认选择失败：" + extension, error);
                }
            }
            Diagnostics.Sink.Log(failed > 0 ? LogSeverity.Warn : LogSeverity.Info, LoggerName,
                "重置旧默认选择完成：已清除 " + cleared + " 项，失败 " + failed + " 项", null);
            // 清除后也要刷新 shell 缓存，否则资源管理器可能继续按旧关联解析。
            NotifyShellOfAssociationChange();
            return new UserChoiceResetResult(cleared, failed);
        }

        // UserChoice 的 ProgId（用户经「打开方式/默认应用」选定），不存在时返回 null。
        private static string ReadUserChoiceProgId(RegistryKey extensionKey)
        {
            using (var userChoice = extensionKey.OpenSubKey("UserChoice"))
            {
                return userChoice == null ? null : userChoice.GetValue("ProgId") as string;
            }
        }

        private string ReadUserChoiceProgId(string extension)
        {
            using (var key = root.OpenSubKey(fileExtsRootPath + "\\" + extension))
            {
                return key == null ? null : ReadUserChoiceProgId(key);
            }
        }

        // 直接把本程序写成各扩展名的默认程序。只有用户明确要求（点「设为默认」/勾选安装选项）时才应调用：
        // 这会覆盖用户此前为图片选定的其他程序。
        public UserChoiceSetResult SetDefaultViewer(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("可执行文件路径不能为空。", "executablePath");

            var applied = 0;
            var failed = 0;
            if (!IsCurrentUserRoot())
            {
                // 默认程序是用户级设置，机器级（HKLM）实例不处理。
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "非当前用户注册表根，跳过设置默认图片程序", null);
                return new UserChoiceSetResult(0, extensions.Length);
            }

            var userSid = UserChoiceHash.GetUserSid();
            if (String.IsNullOrWhiteSpace(userSid))
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "无法取得当前用户 SID，跳过设置默认图片程序", null);
                return new UserChoiceSetResult(0, extensions.Length);
            }

            var now = DateTime.Now;
            foreach (var extension in extensions)
            {
                try
                {
                    var hash = UserChoiceHash.Compute(UserChoiceHash.BuildBaseInfo(extension, userSid, ProgId, now, UserChoiceHash.UserExperience));
                    if (String.IsNullOrEmpty(hash))
                    {
                        failed++;
                        Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "计算默认程序哈希失败：" + extension, null);
                        continue;
                    }

                    var userChoicePath = fileExtsRootPath + "\\" + extension + "\\UserChoice";
                    DeleteUserChoiceKey(userChoicePath);
                    using (var key = root.CreateSubKey(userChoicePath))
                    {
                        key.SetValue("Hash", hash);
                        key.SetValue("ProgId", ProgId);
                    }
                    WriteAssociationToast(extension);

                    // 被系统或安全软件拦截时写入会静默失败，回读后计数才可信。
                    var written = ReadUserChoiceProgId(extension);
                    if (String.Equals(written, ProgId, StringComparison.OrdinalIgnoreCase))
                    {
                        applied++;
                    }
                    else
                    {
                        failed++;
                        Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "设置默认程序后回读不一致：" + extension, null);
                    }
                }
                catch (Exception error)
                {
                    failed++;
                    Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "设置默认图片程序失败：" + extension, error);
                }
            }

            NotifyShellOfAssociationChange();
            Diagnostics.Sink.Log(failed > 0 ? LogSeverity.Warn : LogSeverity.Info, LoggerName,
                "设置默认图片程序完成：成功 " + applied + " 项，失败 " + failed + " 项", null);
            return new UserChoiceSetResult(applied, failed);
        }

        // 取消关联时清掉指向本程序的默认选择；用户选了别的程序时保持不动。
        public int RemoveDefaultViewer(string executablePath)
        {
            if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("可执行文件路径不能为空。", "executablePath");
            if (!IsCurrentUserRoot()) return 0;

            var applicationProgId = ApplicationProgId(executablePath);
            var removed = 0;
            foreach (var extension in extensions)
            {
                if (!IsOurProgId(ReadUserChoiceProgId(extension), applicationProgId)) continue;
                DeleteUserChoiceKey(fileExtsRootPath + "\\" + extension + "\\UserChoice");
                removed++;
            }
            if (removed > 0)
            {
                NotifyShellOfAssociationChange();
                Diagnostics.Sink.Log(LogSeverity.Info, LoggerName, "已清除指向本程序的默认选择：" + removed + " 项", null);
            }
            return removed;
        }

        private bool IsCurrentUserRoot()
        {
            return root.Name.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase);
        }

        // UserChoice 上带有拒绝 SetValue 的保护性 ACE：先打开该键再删除会被拒绝，
        // 用父键句柄调用 RegDeleteKey 才能删掉，重建后的键继承父键 ACL。
        private static void DeleteUserChoiceKey(string relativePath)
        {
            RegDeleteKey(HkeyCurrentUser, relativePath);
        }

        private void WriteAssociationToast(string extension)
        {
            try
            {
                using (var toasts = root.CreateSubKey(softwareRootPath + @"\Microsoft\Windows\CurrentVersion\ApplicationAssociationToasts"))
                {
                    toasts.SetValue(ProgId + "_" + extension, 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "写入关联提示记录失败：" + extension, error);
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

        private static readonly IntPtr HkeyCurrentUser = new IntPtr(unchecked((int)0x80000001));

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegDeleteKey(IntPtr hKey, string subKey);

        // 引导入口：程序已尝试直接写入默认程序，失败时仍可打开系统设置由用户确认。
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

    // 旧默认选择重置结果：Cleared 为已清除项数，Failed 为受系统保护等原因未能清除的项数。
    public sealed class UserChoiceResetResult
    {
        public UserChoiceResetResult(int cleared, int failed)
        {
            Cleared = cleared;
            Failed = failed;
        }

        public int Cleared { get; private set; }
        public int Failed { get; private set; }
    }

    // 设置默认程序的结果：Applied 为已生效项数，Failed 为被系统或安全软件拦截的项数。
    public sealed class UserChoiceSetResult
    {
        public UserChoiceSetResult(int applied, int failed)
        {
            Applied = applied;
            Failed = failed;
        }

        public int Applied { get; private set; }
        public int Failed { get; private set; }
    }
}
