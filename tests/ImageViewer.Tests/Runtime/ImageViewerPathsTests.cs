using System;
using System.IO;
using ImageViewer.Runtime;
using Xunit;

namespace ImageViewer.Tests.Runtime
{
    public sealed class ImageViewerPathsTests
    {
        [Fact]
        public void Logs_live_under_the_appdata_ImageViewer_folder()
        {
            var expectedRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImageViewer");
            var paths = ImageViewerPaths.ForCurrentUser();

            Assert.Equal(expectedRoot, paths.AppDataDirectory);
            Assert.Equal(Path.Combine(expectedRoot, "Logs"), paths.LogsDirectory);
        }

        [Fact]
        public void Config_file_lives_next_to_the_executable()
        {
            var expected = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImageViewer.exe.config");

            Assert.Equal(expected, ImageViewerPaths.ConfigFilePath);
        }

        [Fact]
        public void Paths_do_not_reference_the_main_application_folder()
        {
            var paths = ImageViewerPaths.ForCurrentUser();

            Assert.DoesNotContain("ImageDataViewer", paths.AppDataDirectory);
            Assert.DoesNotContain("ImageDataViewer", paths.LogsDirectory);
            Assert.DoesNotContain("ImageDataViewer", ImageViewerPaths.ConfigFilePath);
        }
    }
}
