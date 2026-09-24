using System;
using System.IO;
using ImageViewer.App.Runtime;
using Xunit;

namespace ImageViewer.App.Tests.Runtime
{
    public sealed class ViewerPreferencesStoreTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "iv-prefs-" + Guid.NewGuid().ToString("N") + ".json");

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
            File.WriteAllText(path, "{ this is not valid json");

            Assert.False(ViewerPreferencesStore.TryLoadDismissed(path));
        }
    }
}
