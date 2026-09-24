using System;
using System.ComponentModel;
using ImageViewer.App.Services;
using Microsoft.Win32;
using Xunit;

namespace ImageViewer.App.Tests.Services
{
    public sealed class ViewerAssociationServiceTests : IDisposable
    {
        private readonly string testRoot = @"Software\ImageViewerAssocTest-" + Guid.NewGuid().ToString("N");
        private readonly string exePath = @"C:\Apps\ImageViewer\ImageViewer.exe";

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
    }
}
