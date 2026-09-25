using System;
using System.IO;
using System.Linq;
using ImageViewer.Viewer;
using Xunit;

namespace ImageViewer.Tests.Viewer
{
    public sealed class ViewerImageDirectoryScannerTests
    {
        [Fact]
        public void Scan_lists_only_whitelisted_images_without_recursing()
        {
            var directory = CreateTempDirectory();
            try
            {
                var jpg = WriteFile(directory, "a.jpg");
                var png = WriteFile(directory, "b.png");
                var bmp = WriteFile(directory, "c.bmp");
                WriteFile(directory, "d.gif");
                WriteFile(directory, "e.txt");
                var sub = Path.Combine(directory, "sub");
                Directory.CreateDirectory(sub);
                WriteFile(sub, "f.jpg");

                var result = ViewerImageDirectoryScanner.Scan(jpg);

                Assert.Equal(new[] { jpg, png, bmp }, result);
                Assert.DoesNotContain(result, path => String.Equals(Path.GetFileName(path), "f.jpg", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                DeleteDirectoryQuietly(directory);
            }
        }

        [Fact]
        public void Scan_sorts_by_file_name_case_insensitively_and_keeps_the_current_image()
        {
            var directory = CreateTempDirectory();
            try
            {
                var upper = WriteFile(directory, "B.jpg");
                var lower = WriteFile(directory, "a.jpg");
                var current = WriteFile(directory, "c.jpg");

                var result = ViewerImageDirectoryScanner.Scan(current);

                Assert.Equal(new[] { lower, upper, current }, result);
                Assert.Equal(2, result.ToList().FindIndex(path => String.Equals(path, current, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                DeleteDirectoryQuietly(directory);
            }
        }

        [Fact]
        public void Scan_sorts_numeric_file_names_in_natural_order()
        {
            var directory = CreateTempDirectory();
            try
            {
                var one = WriteFile(directory, "1.jpg");
                var two = WriteFile(directory, "2.jpg");
                var ten = WriteFile(directory, "10.jpg");
                var eleven = WriteFile(directory, "11.jpg");
                var twenty = WriteFile(directory, "20.jpg");

                var result = ViewerImageDirectoryScanner.Scan(ten);

                Assert.Equal(new[] { one, two, ten, eleven, twenty }, result);
            }
            finally
            {
                DeleteDirectoryQuietly(directory);
            }
        }

        [Fact]
        public void Scan_sorts_mixed_text_and_numbers_naturally()
        {
            var directory = CreateTempDirectory();
            try
            {
                var image2 = WriteFile(directory, "img2.jpg");
                var image10 = WriteFile(directory, "img10.jpg");
                var image20 = WriteFile(directory, "img20.jpg");

                var result = ViewerImageDirectoryScanner.Scan(image2);

                Assert.Equal(new[] { image2, image10, image20 }, result);
            }
            finally
            {
                DeleteDirectoryQuietly(directory);
            }
        }

        [Fact]
        public void Scan_falls_back_to_the_command_line_image_when_the_directory_is_missing()
        {
            var missing = Path.Combine(Path.GetTempPath(), "idv-missing-" + Guid.NewGuid().ToString("N"), "a.jpg");

            var result = ViewerImageDirectoryScanner.Scan(missing);

            Assert.Equal(new[] { missing }, result);
        }

        [Fact]
        public void Scan_falls_back_to_the_command_line_image_when_no_whitelisted_images_exist()
        {
            var directory = CreateTempDirectory();
            try
            {
                WriteFile(directory, "note.txt");
                var current = Path.Combine(directory, "a.jpg");

                var result = ViewerImageDirectoryScanner.Scan(current);

                Assert.Equal(new[] { current }, result);
            }
            finally
            {
                DeleteDirectoryQuietly(directory);
            }
        }

        [Fact]
        public void Scan_returns_empty_for_a_blank_path()
        {
            Assert.Empty(ViewerImageDirectoryScanner.Scan(null));
            Assert.Empty(ViewerImageDirectoryScanner.Scan(""));
            Assert.Empty(ViewerImageDirectoryScanner.Scan("   "));
        }

        [Fact]
        public void EnumerateSiblingDirectories_returns_all_siblings_in_natural_order_including_current()
        {
            var parent = CreateTempDirectory();
            try
            {
                var two = Directory.CreateDirectory(Path.Combine(parent, "2")).FullName;
                var ten = Directory.CreateDirectory(Path.Combine(parent, "10")).FullName;
                var a = Directory.CreateDirectory(Path.Combine(parent, "a")).FullName;
                var b = Directory.CreateDirectory(Path.Combine(parent, "B")).FullName;

                var result = ViewerImageDirectoryScanner.EnumerateSiblingDirectories(ten);

                Assert.Equal(new[] { two, ten, a, b }, result);
            }
            finally
            {
                DeleteDirectoryQuietly(parent);
            }
        }

        [Fact]
        public void EnumerateSiblingDirectories_returns_only_itself_when_parent_has_no_other_subdirectories()
        {
            var parent = CreateTempDirectory();
            try
            {
                var only = Directory.CreateDirectory(Path.Combine(parent, "only")).FullName;

                var result = ViewerImageDirectoryScanner.EnumerateSiblingDirectories(only);

                Assert.Equal(new[] { only }, result);
            }
            finally
            {
                DeleteDirectoryQuietly(parent);
            }
        }

        [Fact]
        public void EnumerateSiblingDirectories_returns_empty_for_a_blank_path()
        {
            Assert.Empty(ViewerImageDirectoryScanner.EnumerateSiblingDirectories(null));
            Assert.Empty(ViewerImageDirectoryScanner.EnumerateSiblingDirectories(""));
            Assert.Empty(ViewerImageDirectoryScanner.EnumerateSiblingDirectories("   "));
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "idv-scan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string WriteFile(string directory, string name)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, "image");
            return path;
        }

        private static void DeleteDirectoryQuietly(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
