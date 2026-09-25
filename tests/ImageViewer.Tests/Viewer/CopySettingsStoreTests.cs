using System;
using System.IO;
using ImageViewer.Viewer;
using Xunit;

namespace ImageViewer.Tests.Viewer
{
    public sealed class CopySettingsStoreTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "iv-copy-" + Guid.NewGuid().ToString("N") + ".config");

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
        public void Load_returns_defaults_when_file_is_missing()
        {
            var settings = CopySettingsStore.Load(path);

            Assert.Empty(settings.Targets);
            Assert.Equal("", settings.TimestampFormat);
            Assert.Equal(CopyConflict.Ask, settings.Conflict);
            Assert.Equal("Space", settings.Shortcut);
        }

        [Fact]
        public void Save_then_Load_round_trips_targets_in_order()
        {
            var settings = new CopySettings();
            settings.Targets.Add(new CopyTarget { Path = @"D:\Out", Suffix = "_a" });
            settings.Targets.Add(new CopyTarget { Path = @"E:\Backup", Suffix = "" });

            CopySettingsStore.Save(path, settings);
            var loaded = CopySettingsStore.Load(path);

            Assert.Equal(2, loaded.Targets.Count);
            Assert.Equal(@"D:\Out", loaded.Targets[0].Path);
            Assert.Equal("_a", loaded.Targets[0].Suffix);
            Assert.Equal(@"E:\Backup", loaded.Targets[1].Path);
        }

        [Fact]
        public void Save_then_Load_round_trips_global_settings()
        {
            var settings = new CopySettings
            {
                TimestampFormat = "yyyyMMdd",
                Conflict = CopyConflict.Overwrite,
                Shortcut = "Enter"
            };

            CopySettingsStore.Save(path, settings);
            var loaded = CopySettingsStore.Load(path);

            Assert.Equal("yyyyMMdd", loaded.TimestampFormat);
            Assert.Equal(CopyConflict.Overwrite, loaded.Conflict);
            Assert.Equal("Enter", loaded.Shortcut);
        }

        [Fact]
        public void Save_then_Load_does_not_carry_stale_targets_after_shrinking()
        {
            var settings = new CopySettings();
            settings.Targets.Add(new CopyTarget { Path = @"D:\1" });
            settings.Targets.Add(new CopyTarget { Path = @"D:\2" });
            CopySettingsStore.Save(path, settings);

            settings.Targets.RemoveAt(1);
            CopySettingsStore.Save(path, settings);
            var loaded = CopySettingsStore.Load(path);

            Assert.Single(loaded.Targets);
        }
    }
}
