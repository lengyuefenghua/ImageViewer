using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageViewer.Services;
using Xunit;

namespace ImageViewer.App.Tests.Services
{
    public sealed class BitmapSourceLoaderTests
    {
        [Fact]
        public void LoadForDisplay_downscales_when_the_decoded_bytes_exceed_the_budget()
        {
            var path = WriteTempPng(100, 50);
            try
            {
                var bitmap = Assert.IsAssignableFrom<BitmapSource>(BitmapSourceLoader.LoadForDisplay(path, 100, 50, maxDecodedBytes: 100));

                Assert.True(bitmap.PixelWidth < 100);
                Assert.True(bitmap.PixelWidth > 0);
                Assert.True(bitmap.PixelHeight > 0);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void LoadForDisplay_keeps_the_native_size_when_within_the_decoded_byte_budget()
        {
            var path = WriteTempPng(100, 50);
            try
            {
                var bitmap = Assert.IsAssignableFrom<BitmapSource>(BitmapSourceLoader.LoadForDisplay(path, 100, 50, maxDecodedBytes: 1610612736L));

                Assert.Equal(100, bitmap.PixelWidth);
                Assert.Equal(50, bitmap.PixelHeight);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Theory]
        [InlineData(".png")]
        [InlineData(".jpg")]
        [InlineData(".bmp")]
        public void ReadDimensions_returns_the_real_size_for_supported_formats(string extension)
        {
            var path = WriteTempImage(extension, 100, 50);
            try
            {
                var size = BitmapSourceLoader.ReadDimensions(path);

                Assert.Equal(100, size.Width);
                Assert.Equal(50, size.Height);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void LoadForDisplay_releases_the_file_while_the_bitmap_is_alive()
        {
            var path = WriteTempPng(10, 10);
            try
            {
                var bitmap = BitmapSourceLoader.LoadForDisplay(path, 10, 10);

                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }

                GC.KeepAlive(bitmap);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void Load_releases_the_file_while_the_bitmap_is_alive()
        {
            var path = WriteTempPng(10, 10);
            try
            {
                var bitmap = BitmapSourceLoader.Load(path);

                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }

                GC.KeepAlive(bitmap);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void ReadDimensions_releases_the_file_after_returning()
        {
            var path = WriteTempPng(10, 10);
            try
            {
                BitmapSourceLoader.ReadDimensions(path);

                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void ReadDimensions_throws_for_non_image_content()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllText(path, "this is not an image");
            try
            {
                Assert.ThrowsAny<Exception>(() => BitmapSourceLoader.ReadDimensions(path));
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void ReadDimensions_throws_for_an_empty_path()
        {
            Assert.Throws<ArgumentException>(() => BitmapSourceLoader.ReadDimensions(""));
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

        private static string WriteTempPng(int width, int height)
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = File.Create(path)) encoder.Save(stream);
            return path;
        }

        private static string WriteTempImage(string extension, int width, int height)
        {
            if (extension == ".png") return WriteTempPng(width, height);
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
            using (var bitmap = new Bitmap(width, height))
            {
                bitmap.Save(path, extension == ".jpg" ? ImageFormat.Jpeg : ImageFormat.Bmp);
            }
            return path;
        }
    }
}
