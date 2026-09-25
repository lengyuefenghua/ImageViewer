using System;
using System.IO;
using ImageViewer.Runtime;
using ImageViewer.Viewer;
using Xunit;

namespace ImageViewer.Tests.Runtime
{
    public sealed class ViewerPreferencesStoreTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "iv-prefs-" + Guid.NewGuid().ToString("N") + ".config");

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
        public void SaveDismissed_then_TryLoad_round_trips()
        {
            ViewerPreferencesStore.SaveDismissed(path, true);

            Assert.True(ViewerPreferencesStore.TryLoadDismissed(path));
        }

        [Fact]
        public void TryLoadDismissed_is_false_when_file_missing()
        {
            Assert.False(ViewerPreferencesStore.TryLoadDismissed(path));
        }

        [Fact]
        public void TryLoadDismissed_is_false_when_file_corrupt()
        {
            File.WriteAllText(path, "<configuration><appSettings><broken>");

            Assert.False(ViewerPreferencesStore.TryLoadDismissed(path));
        }

        [Fact]
        public void SaveDismissed_preserves_window_state_already_stored_in_the_same_config_file()
        {
            ViewerWindowStateStore.Save(path, new ViewerWindowState { Width = 1024, Height = 768, Left = 20, Top = 30 });

            ViewerPreferencesStore.SaveDismissed(path, true);

            var state = ViewerWindowStateStore.TryLoad(path);
            Assert.NotNull(state);
            Assert.Equal(1024, state.Width, 3);
            Assert.Equal(768, state.Height, 3);
        }
    }
}
