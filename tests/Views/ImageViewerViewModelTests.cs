using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using ImageViewer.Views;
using Xunit;

namespace ImageViewer.Tests.Views
{
    public sealed class ImageViewerViewModelTests
    {
        [Fact]
        public void SetResultSet_sets_count_and_resets_position()
        {
            var viewModel = new ImageViewerViewModel();
            viewModel.SetResultSet(new[] { "a.jpg", "b.jpg", "c.jpg" });

            Assert.Equal(3, viewModel.ResultCount);
            Assert.Equal(0, viewModel.CurrentPosition);
        }

        [Fact]
        public void OpenAt_positions_within_bounds()
        {
            var path = WriteTempImage(200, 100);
            try
            {
                var viewModel = new ImageViewerViewModel();
                viewModel.SetResultSet(new[] { path, path });

                viewModel.OpenAt(1, 200, 100, 100, 100);

                Assert.Equal(2, viewModel.CurrentPosition);
                Assert.Equal(200, viewModel.ImageWidth);
                Assert.Equal(100, viewModel.ImageHeight);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void OpenAt_throws_for_out_of_range_index()
        {
            var viewModel = new ImageViewerViewModel();
            viewModel.SetResultSet(new[] { "a.jpg" });

            Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.OpenAt(1, 10, 10, 100, 100));
        }

        [Fact]
        public async Task Next_and_Previous_stay_within_bounds()
        {
            var path = WriteTempImage(50, 50);
            try
            {
                var viewModel = new ImageViewerViewModel();
                viewModel.SetResultSet(new[] { path, path });
                viewModel.OpenAt(0, 50, 50, 100, 100);

                await viewModel.PreviousAsync();
                Assert.Equal(1, viewModel.CurrentPosition);

                await viewModel.NextAsync();
                Assert.Equal(2, viewModel.CurrentPosition);

                await viewModel.NextAsync();
                Assert.Equal(2, viewModel.CurrentPosition);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void ZoomAt_then_FitToWindow_restores_fit_scale()
        {
            var path = WriteTempImage(200, 100);
            try
            {
                var viewModel = new ImageViewerViewModel();
                viewModel.SetResultSet(new[] { path });
                viewModel.OpenAt(0, 200, 100, 100, 100);

                Assert.Equal(0.5, viewModel.Zoom, 3);

                viewModel.ZoomAt(new PointF(50, 50), 1.0);
                Assert.Equal(1.0, viewModel.Zoom, 3);

                viewModel.FitToWindow();
                Assert.Equal(0.5, viewModel.Zoom, 3);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void Pointer_outside_image_shows_placeholder()
        {
            var path = WriteTempImage(100, 100);
            try
            {
                var viewModel = new ImageViewerViewModel();
                viewModel.SetResultSet(new[] { path });
                viewModel.OpenAt(0, 100, 100, 100, 100);

                viewModel.SetPointerPosition(new PointF(-50, -50));

                Assert.Equal(-1, viewModel.PointerX);
                Assert.Equal(-1, viewModel.PointerY);
                Assert.Equal(-1, viewModel.PixelR);
                Assert.Equal(-1, viewModel.PixelG);
                Assert.Equal(-1, viewModel.PixelB);
            }
            finally
            {
                DeleteQuietly(path);
            }
        }

        [Fact]
        public void SetFileInfo_updates_size_and_modified_display_including_seconds()
        {
            var viewModel = new ImageViewerViewModel();
            var changed = new List<string>();
            PropertyChangedEventHandler handler = (sender, args) => changed.Add(args.PropertyName);
            viewModel.PropertyChanged += handler;

            viewModel.SetFileInfo(1536, new DateTime(2026, 9, 1, 8, 1, 42, DateTimeKind.Local));

            Assert.Equal("1.50 KB", viewModel.ImageSizeDisplay);
            Assert.Equal("2026-09-01 08:01:42", viewModel.ImageModifiedDisplay);
            Assert.Contains("ImageSizeDisplay", changed);
            Assert.Contains("ImageModifiedDisplay", changed);
        }

        [Fact]
        public void ShowMissingFile_sets_error_and_clears_dimensions()
        {
            var viewModel = new ImageViewerViewModel();
            viewModel.SetResultSet(new[] { "C:\\not-here\\missing.jpg" });

            viewModel.ShowMissingFile("C:\\not-here\\missing.jpg");

            Assert.Contains("文件打开失败", viewModel.ErrorMessage);
            Assert.Contains("missing.jpg", viewModel.ErrorMessage);
            Assert.Equal(0, viewModel.ImageWidth);
            Assert.Equal(0, viewModel.ImageHeight);
            Assert.Equal(1.0, viewModel.Zoom, 3);
            Assert.False(viewModel.IsLoading);
        }

        private static string WriteTempImage(int width, int height)
        {
            var path = Path.Combine(Path.GetTempPath(), "iv-vm-" + Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.CornflowerBlue);
                bitmap.Save(path, ImageFormat.Png);
            }

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
