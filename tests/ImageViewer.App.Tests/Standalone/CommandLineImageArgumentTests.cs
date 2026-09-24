using System;
using System.IO;
using ImageViewer.App.Standalone;
using Xunit;

namespace ImageViewer.App.Tests.Standalone
{
    public sealed class CommandLineImageArgumentTests
    {
        [Fact]
        public void TryResolve_returns_the_full_path_for_an_existing_jpg()
        {
            var path = WriteTempImage(".jpg");
            try
            {
                string resolved;
                Assert.True(CommandLineImageArgument.TryResolve(new[] { path }, out resolved));
                Assert.Equal(path, resolved);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Theory]
        [InlineData(".JPG")]
        [InlineData(".PNG")]
        [InlineData(".BMP")]
        [InlineData(".JpG")]
        public void TryResolve_accepts_whitelisted_extensions_case_insensitively(string extension)
        {
            var path = WriteTempImage(extension);
            try
            {
                string resolved;
                Assert.True(CommandLineImageArgument.TryResolve(new[] { path }, out resolved));
                Assert.Equal(path, resolved);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void TryResolve_returns_false_when_the_file_does_not_exist()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".jpg");
            string resolved;
            Assert.False(CommandLineImageArgument.TryResolve(new[] { path }, out resolved));
            Assert.Null(resolved);
        }

        [Theory]
        [InlineData(".gif")]
        [InlineData(".txt")]
        [InlineData("")]
        public void TryResolve_returns_false_for_non_whitelisted_extensions(string extension)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
            File.WriteAllText(path, "not an image");
            try
            {
                string resolved;
                Assert.False(CommandLineImageArgument.TryResolve(new[] { path }, out resolved));
                Assert.Null(resolved);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void TryResolve_returns_false_for_null_or_empty_arguments()
        {
            string resolved;
            Assert.False(CommandLineImageArgument.TryResolve(null, out resolved));
            Assert.False(CommandLineImageArgument.TryResolve(new string[0], out resolved));
            Assert.False(CommandLineImageArgument.TryResolve(new[] { "", "   " }, out resolved));
        }

        [Fact]
        public void TryResolve_uses_the_first_valid_image_when_multiple_arguments_exist()
        {
            var first = WriteTempImage(".png");
            var second = WriteTempImage(".bmp");
            try
            {
                string resolved;
                Assert.True(CommandLineImageArgument.TryResolve(new[] { "missing.jpg", first, second }, out resolved));
                Assert.Equal(first, resolved);
            }
            finally
            {
                DeleteQuietly(first);
                DeleteQuietly(second);
            }
        }

        [Fact]
        public void TryResolve_normalizes_a_relative_path()
        {
            var name = "cli-relative-" + Guid.NewGuid().ToString("N") + ".jpg";
            File.WriteAllText(name, "image");
            try
            {
                string resolved;
                Assert.True(CommandLineImageArgument.TryResolve(new[] { name }, out resolved));
                Assert.Equal(Path.GetFullPath(name), resolved);
            }
            finally
            {
                DeleteQuietly(name);
            }
        }

        private static string WriteTempImage(string extension)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
            File.WriteAllText(path, "image");
            return path;
        }

        private static void DeleteQuietly(string path)
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
}
