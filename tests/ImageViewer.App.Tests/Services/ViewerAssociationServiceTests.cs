using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using ImageViewer.App.Services;
using Microsoft.Win32;
using Xunit;

namespace ImageViewer.App.Tests.Services
{
    public sealed class ViewerAssociationServiceTests : IDisposable
    {
        private readonly string testRoot = @"Software\ImageViewerAssocTest-" + Guid.NewGuid().ToString("N");
        private readonly string exePath = @"C:\Apps\ImageViewer\ImageViewer.exe";
        private readonly List<string> preferenceFiles = new List<string>();

        public void Dispose()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(testRoot, false);
            }
            catch (Exception)
            {
            }

            foreach (var path in preferenceFiles)
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (IOException)
                {
                }
            }
        }

        private string NewPreferencesPath()
        {
            var path = Path.Combine(Path.GetTempPath(), "iv-prefs-" + Guid.NewGuid().ToString("N") + ".json");
            preferenceFiles.Add(path);
            return path;
        }

        private FileAssociationRegistrar AdminRegistrar()
        {
            return new FileAssociationRegistrar(() => true, argument => 0);
        }

        private FileAssociationService CreateMachine() { return new FileAssociationService(Registry.CurrentUser, testRoot); }

        private FileAssociationService CreateUser() { return new FileAssociationService(Registry.CurrentUser, testRoot, testRoot + @"\FileExts"); }

        [Fact]
        public void Register_uses_elevation_when_not_administrator()
        {
            var machine = CreateMachine();
            var user = CreateUser();
            var registrar = new FileAssociationRegistrar(
                () => false,
                argument => { machine.Register(exePath); return 0; });
            var service = new ViewerAssociationService(machine, user, registrar, () => exePath);

            var status = service.Register();

            Assert.True(machine.IsRegistered(exePath));
            Assert.True(service.IsRegistered);
            Assert.Contains("已注册", status);
        }

        [Fact]
        public void Register_registers_directly_when_administrator()
        {
            var machine = CreateMachine();
            var user = CreateUser();
            var elevatedCalled = false;
            var registrar = new FileAssociationRegistrar(
                () => true,
                argument => { elevatedCalled = true; return 0; });
            var service = new ViewerAssociationService(machine, user, registrar, () => exePath);

            var status = service.Register();

            Assert.True(machine.IsRegistered(exePath));
            Assert.False(elevatedCalled);
            Assert.Contains("已注册", status);
        }

        [Fact]
        public void Register_reports_failure_when_elevated_process_returns_non_zero()
        {
            var machine = CreateMachine();
            var user = CreateUser();
            var registrar = new FileAssociationRegistrar(() => false, argument => 1);
            var service = new ViewerAssociationService(machine, user, registrar, () => exePath);

            var status = service.Register();

            Assert.False(machine.IsRegistered(exePath));
            Assert.Contains("注册失败", status);
        }

        [Fact]
        public void Register_reports_cancelled_elevation()
        {
            var machine = CreateMachine();
            var user = CreateUser();
            var registrar = new FileAssociationRegistrar(
                () => false,
                argument => { throw new Win32Exception(1223); });
            var service = new ViewerAssociationService(machine, user, registrar, () => exePath);

            var status = service.Register();

            Assert.Contains("取消", status);
            Assert.False(machine.IsRegistered(exePath));
        }

        [Fact]
        public void Unregister_removes_what_register_wrote()
        {
            var machine = CreateMachine();
            var user = CreateUser();
            var registrar = new FileAssociationRegistrar(() => true, argument => 0);
            var service = new ViewerAssociationService(machine, user, registrar, () => exePath);
            service.Register();

            var status = service.Unregister();

            Assert.False(machine.IsRegistered(exePath));
            Assert.False(service.IsRegistered);
            Assert.Contains("已取消", status);
        }

        [Fact]
        public void ShouldPromptForDefaultViewer_is_true_when_not_dismissed_not_default_and_not_registered()
        {
            var service = new ViewerAssociationService(CreateMachine(), CreateUser(), AdminRegistrar(), () => exePath, NewPreferencesPath());

            Assert.True(service.ShouldPromptForDefaultViewer());
        }

        [Fact]
        public void ShouldPromptForDefaultViewer_is_false_when_registered_for_current_exe()
        {
            var machine = CreateMachine();
            machine.Register(exePath);
            var service = new ViewerAssociationService(machine, CreateUser(), AdminRegistrar(), () => exePath, NewPreferencesPath());

            Assert.False(service.ShouldPromptForDefaultViewer());
        }

        [Fact]
        public void DismissDefaultViewerPrompt_persists_and_stops_prompting()
        {
            var preferencesPath = NewPreferencesPath();
            var service = new ViewerAssociationService(CreateMachine(), CreateUser(), AdminRegistrar(), () => exePath, preferencesPath);

            service.DismissDefaultViewerPrompt();
            var reopened = new ViewerAssociationService(CreateMachine(), CreateUser(), AdminRegistrar(), () => exePath, preferencesPath);

            Assert.False(reopened.ShouldPromptForDefaultViewer());
        }
    }
}
