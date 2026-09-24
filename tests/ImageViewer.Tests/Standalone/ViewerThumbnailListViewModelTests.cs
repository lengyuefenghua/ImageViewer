using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImageViewer.Standalone;
using Xunit;

namespace ImageViewer.Tests.Standalone
{
    public sealed class ViewerThumbnailListViewModelTests
    {
        [Fact]
        public void Items_are_built_from_the_paths_with_index_and_file_name()
        {
            var paths = new[] { @"C:\imgs\1.jpg", @"C:\imgs\2.jpg" };

            using (var viewModel = new ViewerThumbnailListViewModel(paths, LoadNothing))
            {
                Assert.Equal(2, viewModel.Items.Count);
                Assert.Equal(1, viewModel.Items[0].Index);
                Assert.Equal("1.jpg", viewModel.Items[0].FileName);
                Assert.Equal(@"C:\imgs\2.jpg", viewModel.Items[1].Path);
                Assert.True(viewModel.IsVisible);
            }
        }

        [Fact]
        public async Task SetCurrentIndex_marks_the_item_and_loads_it_first()
        {
            var loaded = new List<string>();
            var paths = new[] { "a.jpg", "b.jpg", "c.jpg" };

            // 串行加载保证顺序确定：当前项（b）必须最先加载。
            using (var viewModel = new ViewerThumbnailListViewModel(paths, path =>
            {
                lock (loaded) loaded.Add(path);
                return Task.FromResult<object>(new object());
            }, maxConcurrentLoads: 1))
            {
                viewModel.SetCurrentIndex(1);
                await viewModel.WaitForPendingLoadsAsync();

                Assert.True(viewModel.Items[1].IsCurrent);
                Assert.False(viewModel.Items[0].IsCurrent);
                Assert.Equal("b.jpg", loaded[0]);
                Assert.NotNull(viewModel.Items[1].Bitmap);
            }
        }

        [Fact]
        public void ToggleVisibility_flips_the_visibility()
        {
            using (var viewModel = new ViewerThumbnailListViewModel(new[] { "a.jpg" }, LoadNothing))
            {
                Assert.True(viewModel.IsVisible);

                viewModel.ToggleVisibility();
                Assert.False(viewModel.IsVisible);

                viewModel.ToggleVisibility();
                Assert.True(viewModel.IsVisible);
            }
        }

        [Fact]
        public async Task Cache_evicts_the_least_recently_used_item_beyond_the_capacity()
        {
            var paths = new[] { "a.jpg", "b.jpg" };

            // 串行加载（maxConcurrentLoads: 1）使加载顺序确定：a 先入缓存，b 入缓存时淘汰 a。
            using (var viewModel = new ViewerThumbnailListViewModel(paths, path => Task.FromResult<object>(new object()), cacheCapacity: 1, maxConcurrentLoads: 1))
            {
                viewModel.SetCurrentIndex(0);
                await viewModel.WaitForPendingLoadsAsync();

                Assert.Equal(1, viewModel.CachedItemCount);
                Assert.True(viewModel.IsCached("b.jpg"));
                Assert.False(viewModel.IsCached("a.jpg"));
            }
        }

        [Fact]
        public async Task Failed_thumbnail_is_marked_as_placeholder_and_not_retried()
        {
            var attempts = 0;
            var paths = new[] { "bad.jpg", "good.jpg" };

            using (var viewModel = new ViewerThumbnailListViewModel(paths, path =>
            {
                if (path == "bad.jpg")
                {
                    attempts++;
                    throw new InvalidOperationException("decode failed");
                }
                return Task.FromResult<object>(new object());
            }))
            {
                viewModel.SetCurrentIndex(0);
                await viewModel.WaitForPendingLoadsAsync();
                viewModel.SetCurrentIndex(1);
                await viewModel.WaitForPendingLoadsAsync();

                Assert.True(viewModel.Items[0].IsPlaceholder);
                Assert.Null(viewModel.Items[0].Bitmap);
                Assert.Equal(1, attempts);
            }
        }

        [Fact]
        public async Task Dispose_cancels_pending_loads_and_clears_the_cache()
        {
            var loads = 0;
            var paths = new[] { "a.jpg", "b.jpg" };
            var viewModel = new ViewerThumbnailListViewModel(paths, path =>
            {
                loads++;
                return Task.FromResult<object>(new object());
            });
            viewModel.SetCurrentIndex(0);
            await viewModel.WaitForPendingLoadsAsync();
            Assert.True(viewModel.CachedItemCount > 0);

            viewModel.Dispose();
            var afterDispose = loads;
            viewModel.SetCurrentIndex(1);
            await viewModel.WaitForPendingLoadsAsync();

            Assert.Equal(0, viewModel.CachedItemCount);
            Assert.Equal(afterDispose, loads);
        }

        private static Task<object> LoadNothing(string path)
        {
            return Task.FromResult<object>(null);
        }
    }
}
