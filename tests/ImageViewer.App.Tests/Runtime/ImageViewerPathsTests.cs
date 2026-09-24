using System;
using System.IO;
using ImageViewer.App.Runtime;
using Xunit;

namespace ImageViewer.App.Tests.Runtime
{
    public sealed class ImageViewerPathsTests
    {
        [Fact]
        public void Configuration_and_logs_live_under_the_appdata_ImageViewer_folder()
        {
            var expectedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImageViewer");
            var paths = ImageViewerPaths.ForCurrentUser();

            Assert.Equal(expectedRoot, paths.AppDataDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "Configuration"), paths.ConfigurationDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "Logs"), paths.LogsDirectory);
        }

        [Fact]
        public void Paths_do_not_reference_the_main_application_folder()
        {
            var paths = ImageViewerPaths.ForCurrentUser();

            Assert.DoesNotContain("ImageDataViewer", paths.AppDataDirectory);
            Assert.DoesNotContain("ImageDataViewer", paths.ConfigurationDirectory);
            Assert.DoesNotContain("ImageDataViewer", paths.LogsDirectory);
        }
    }
}
