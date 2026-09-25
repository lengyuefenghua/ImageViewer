using System;
using System.IO;
using ImageViewer.Runtime;
using ImageViewer.Viewer;
using Xunit;

namespace ImageViewer.Tests.Viewer
{
    public sealed class ViewerWindowStateStoreTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "idv-window-" + Guid.NewGuid().ToString("N") + ".config");

        public void Dispose()
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void Save_and_load_round_trip_the_window_state()
        {
            ViewerWindowStateStore.Save(path, new ViewerWindowState
            {
                Width = 1200,
                Height = 900,
                Left = 100,
                Top = 50,
                IsMaximized = true
            });

            var state = ViewerWindowStateStore.TryLoad(path);

            Assert.NotNull(state);
            Assert.Equal(1200, state.Width, 3);
            Assert.Equal(900, state.Height, 3);
            Assert.Equal(100, state.Left, 3);
            Assert.Equal(50, state.Top, 3);
            Assert.True(state.IsMaximized);
        }

        [Fact]
        public void TryLoad_returns_null_when_the_file_is_missing()
        {
            Assert.Null(ViewerWindowStateStore.TryLoad(path));
        }

        [Fact]
        public void TryLoad_returns_null_when_the_file_is_corrupt()
        {
            File.WriteAllText(path, "<configuration><appSettings><broken>");

            Assert.Null(ViewerWindowStateStore.TryLoad(path));
        }

        [Fact]
        public void Save_preserves_preferences_already_stored_in_the_same_config_file()
        {
            ViewerPreferencesStore.SaveDismissed(path, true);

            ViewerWindowStateStore.Save(path, new ViewerWindowState { Width = 800, Height = 600, Left = 10, Top = 10 });

            Assert.True(ViewerPreferencesStore.TryLoadDismissed(path));
        }

        [Theory]
        [InlineData(100, 100, 800, 600, true)]
        [InlineData(0, 0, 800, 600, true)]
        [InlineData(1900, 1000, 800, 600, true)]
        [InlineData(-7900, 100, 800, 600, false)]
        [InlineData(100, -7900, 800, 600, false)]
        public void IsOnScreen_validates_positions_against_the_virtual_screen(
            double left,
            double top,
            double width,
            double height,
            bool expected)
        {
            Assert.Equal(expected, ViewerWindowStateStore.IsOnScreen(left, top, width, height, 0, 0, 1920, 1080));
        }
    }
}
