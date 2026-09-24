using System;
using ImageViewer.App.Services;
using Microsoft.Win32;
using Xunit;

namespace ImageViewer.App.Tests.Services
{
    public sealed class FileAssociationServiceTests : IDisposable
    {
        private readonly string testRoot = @"Software\ImageViewerTest-" + Guid.NewGuid().ToString("N");
        private readonly string exePath = @"C:\Apps\ImageViewer\ImageViewer.exe";
        private readonly string classesRoot;
        private readonly string registeredApplications;

        public FileAssociationServiceTests()
        {
            classesRoot = testRoot + @"\Classes";
            registeredApplications = testRoot + @"\RegisteredApplications";
        }

        public void Dispose()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(testRoot, false);
            }
            catch (Exception)
            {
            }
        }

        [Fact]
        public void IsRegistered_is_false_before_registration()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);

            Assert.False(service.IsRegistered(exePath));
        }

        [Fact]
        public void Register_writes_open_command_supported_types_candidates_and_app_declarations()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);

            service.Register(exePath);

            using (var command = Registry.CurrentUser.OpenSubKey(classesRoot + @"\Applications\ImageViewer.exe\shell\open\command"))
            {
                Assert.NotNull(command);
                Assert.Equal("\"" + exePath + "\" \"%1\"", command.GetValue(null));
            }

            using (var types = Registry.CurrentUser.OpenSubKey(classesRoot + @"\Applications\ImageViewer.exe\SupportedTypes"))
            {
                Assert.NotNull(types);
                Assert.NotNull(types.GetValue(".jpg"));
                Assert.NotNull(types.GetValue(".png"));
                Assert.NotNull(types.GetValue(".bmp"));
            }

            foreach (var extension in new[] { ".jpg", ".png", ".bmp" })
            {
                using (var openWith = Registry.CurrentUser.OpenSubKey(classesRoot + "\\" + extension + @"\OpenWithProgids"))
                {
                    Assert.NotNull(openWith);
                    Assert.NotNull(openWith.GetValue(FileAssociationService.ProgId));
                }
            }

            using (var progId = Registry.CurrentUser.OpenSubKey(classesRoot + "\\" + FileAssociationService.ProgId + @"\shell\open\command"))
            {
                Assert.NotNull(progId);
                Assert.Equal("\"" + exePath + "\" \"%1\"", progId.GetValue(null));
            }

            // 「打开方式」列表显示应用名需要 ProgID 的 Application 子键。
            using (var application = Registry.CurrentUser.OpenSubKey(classesRoot + "\\" + FileAssociationService.ProgId + @"\Application"))
            {
                Assert.NotNull(application);
                Assert.Equal(FileAssociationService.FriendlyAppName, application.GetValue("ApplicationName"));
            }

            // 「默认应用」列表需要 RegisteredApplications 指向 Capabilities\FileAssociations。
            using (var registered = Registry.CurrentUser.OpenSubKey(registeredApplications))
            {
                Assert.NotNull(registered);
                Assert.Equal(testRoot + @"\ImageViewer\Capabilities", registered.GetValue(FileAssociationService.FriendlyAppName));
            }

            using (var capabilities = Registry.CurrentUser.OpenSubKey(testRoot + @"\ImageViewer\Capabilities"))
            {
                Assert.NotNull(capabilities);
                Assert.Equal(FileAssociationService.FriendlyAppName, capabilities.GetValue("ApplicationName"));
            }

            using (var fileAssociations = Registry.CurrentUser.OpenSubKey(testRoot + @"\ImageViewer\Capabilities\FileAssociations"))
            {
                Assert.NotNull(fileAssociations);
                Assert.Equal(FileAssociationService.ProgId, fileAssociations.GetValue(".jpg"));
                Assert.Equal(FileAssociationService.ProgId, fileAssociations.GetValue(".png"));
                Assert.Equal(FileAssociationService.ProgId, fileAssociations.GetValue(".bmp"));
            }

            Assert.True(service.IsRegistered(exePath));
        }

        [Fact]
        public void RegisterUserCandidates_writes_the_progid_into_file_exts_open_with_progids()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot + @"\Software", testRoot + @"\FileExts");

            service.RegisterUserCandidates(exePath);

            foreach (var extension in new[] { ".jpg", ".png", ".bmp" })
            {
                using (var openWith = Registry.CurrentUser.OpenSubKey(testRoot + @"\FileExts\" + extension + @"\OpenWithProgids"))
                {
                    Assert.NotNull(openWith);
                    Assert.NotNull(openWith.GetValue(FileAssociationService.ProgId));
                }
            }
        }

        [Fact]
        public void UnregisterUserCandidates_removes_the_progid_from_file_exts()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot + @"\Software", testRoot + @"\FileExts");
            service.RegisterUserCandidates(exePath);

            service.UnregisterUserCandidates(exePath);

            foreach (var extension in new[] { ".jpg", ".png", ".bmp" })
            {
                using (var openWith = Registry.CurrentUser.OpenSubKey(testRoot + @"\FileExts\" + extension + @"\OpenWithProgids"))
                {
                    if (openWith != null)
                    {
                        Assert.Null(openWith.GetValue(FileAssociationService.ProgId));
                    }
                }
            }
        }

        [Fact]
        public void Register_adds_an_open_verb_under_the_system_progid_of_each_extension()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);
            using (var jpg = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\.jpg")) jpg.SetValue(null, "jpegfile");
            using (var png = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\.png")) png.SetValue(null, "pngfile");
            using (var bmp = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\.bmp")) bmp.SetValue(null, "Paint.Picture");

            service.Register(exePath);

            foreach (var pair in new[]
            {
                new[] { ".jpg", "jpegfile" },
                new[] { ".png", "pngfile" },
                new[] { ".bmp", "Paint.Picture" }
            })
            {
                using (var verb = Registry.CurrentUser.OpenSubKey(testRoot + @"\Classes\" + pair[1] + @"\shell\" + FileAssociationService.OpenVerbName + @"\command"))
                {
                    Assert.NotNull(verb);
                    Assert.Equal("\"" + exePath + "\" \"%1\"", verb.GetValue(null));
                }
            }
        }

        [Fact]
        public void Register_hijacks_the_default_shell_verb_and_backs_up_the_original()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);
            using (var jpg = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\.jpg")) jpg.SetValue(null, "jpegfile");
            using (var shell = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\jpegfile\shell")) shell.SetValue(null, "open");

            service.Register(exePath);

            using (var shell = Registry.CurrentUser.OpenSubKey(testRoot + @"\Classes\jpegfile\shell"))
            {
                Assert.Equal(FileAssociationService.OpenVerbName, shell.GetValue(null));
            }
            using (var backup = Registry.CurrentUser.OpenSubKey(testRoot + @"\ImageViewer\Backup"))
            {
                Assert.NotNull(backup);
                Assert.Equal("open", backup.GetValue("jpegfile"));
            }
        }

        [Fact]
        public void Register_is_idempotent_and_keeps_the_first_backup()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);
            using (var jpg = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\.jpg")) jpg.SetValue(null, "jpegfile");
            using (var shell = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\jpegfile\shell")) shell.SetValue(null, "open");

            service.Register(exePath);
            service.Register(exePath);

            using (var backup = Registry.CurrentUser.OpenSubKey(testRoot + @"\ImageViewer\Backup"))
            {
                Assert.Equal("open", backup.GetValue("jpegfile"));
            }
        }

        [Fact]
        public void Unregister_restores_the_backed_up_default_verb()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);
            using (var jpg = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\.jpg")) jpg.SetValue(null, "jpegfile");
            using (var shell = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\jpegfile\shell")) shell.SetValue(null, "open");
            service.Register(exePath);

            service.Unregister(exePath);

            using (var shell = Registry.CurrentUser.OpenSubKey(testRoot + @"\Classes\jpegfile\shell"))
            {
                Assert.Equal("open", shell.GetValue(null));
            }
            Assert.Null(Registry.CurrentUser.OpenSubKey(testRoot + @"\ImageViewer\Backup"));
        }

        [Fact]
        public void Register_sets_the_progid_friendly_description()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);

            service.Register(exePath);

            using (var progId = Registry.CurrentUser.OpenSubKey(classesRoot + "\\" + FileAssociationService.ProgId))
            {
                Assert.NotNull(progId);
                Assert.False(String.IsNullOrWhiteSpace(progId.GetValue(null) as string));
            }
        }

        [Fact]
        public void IsRegisteredFor_matches_only_the_current_executable_path()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);
            service.Register(exePath);

            Assert.True(service.IsRegisteredFor(exePath));
            Assert.False(service.IsRegisteredFor(@"C:\Other\ImageViewer.exe"));
        }

        [Fact]
        public void IsDefaultViewer_is_false_without_a_user_choice()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot, testRoot + @"\FileExts");

            Assert.False(service.IsDefaultViewer());
        }

        [Fact]
        public void IsDefaultViewer_is_true_when_any_extension_user_choice_points_to_our_progid()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot, testRoot + @"\FileExts");
            using (var userChoice = Registry.CurrentUser.CreateSubKey(testRoot + @"\FileExts\.jpg\UserChoice"))
            {
                userChoice.SetValue("ProgId", FileAssociationService.ProgId);
            }

            Assert.True(service.IsDefaultViewer());
        }

        [Fact]
        public void Register_is_idempotent()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);

            service.Register(exePath);
            service.Register(exePath);

            Assert.True(service.IsRegistered(exePath));
        }

        [Fact]
        public void Unregister_removes_everything_it_wrote()
        {
            var service = new FileAssociationService(Registry.CurrentUser, testRoot);
            using (var jpg = Registry.CurrentUser.CreateSubKey(testRoot + @"\Classes\.jpg")) jpg.SetValue(null, "jpegfile");
            service.Register(exePath);

            service.Unregister(exePath);

            Assert.False(service.IsRegistered(exePath));
            Assert.Null(Registry.CurrentUser.OpenSubKey(testRoot + @"\Classes\jpegfile\shell\" + FileAssociationService.OpenVerbName));
            Assert.Null(Registry.CurrentUser.OpenSubKey(classesRoot + @"\Applications\ImageViewer.exe"));
            Assert.Null(Registry.CurrentUser.OpenSubKey(classesRoot + "\\" + FileAssociationService.ProgId));
            Assert.Null(Registry.CurrentUser.OpenSubKey(testRoot + @"\ImageViewer\Capabilities"));
            using (var registered = Registry.CurrentUser.OpenSubKey(registeredApplications))
            {
                if (registered != null)
                {
                    Assert.Null(registered.GetValue(FileAssociationService.FriendlyAppName));
                }
            }
            foreach (var extension in new[] { ".jpg", ".png", ".bmp" })
            {
                using (var openWith = Registry.CurrentUser.OpenSubKey(classesRoot + "\\" + extension + @"\OpenWithProgids"))
                {
                    if (openWith != null)
                    {
                        Assert.Null(openWith.GetValue(FileAssociationService.ProgId));
                    }
                }
            }
        }
    }
}
