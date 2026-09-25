using System;
using System.IO;
using ImageViewer.Services;
using Xunit;

namespace ImageViewer.Tests.Services
{
    public sealed class ImageCopyServiceTests : IDisposable
    {
        private readonly string root;

        public ImageCopyServiceTests()
        {
            root = Path.Combine(Path.GetTempPath(), "iv-copysvc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }

        private string CreateSource(string name = "photo.jpg", string content = "src")
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void Copy_copies_file_into_target_directory()
        {
            var source = CreateSource();
            var targetDir = Path.Combine(root, "out");

            var result = ImageCopyService.Copy(source, targetDir, "photo_x.jpg", CopyConflict.Ask, null);

            Assert.Equal(CopyResult.Copied, result);
            Assert.True(File.Exists(Path.Combine(targetDir, "photo_x.jpg")));
        }

        [Fact]
        public void Copy_overwrites_when_conflict_policy_is_overwrite()
        {
            var source = CreateSource(content: "new");
            var targetDir = Path.Combine(root, "out");
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(targetDir, "photo.jpg"), "old");

            var result = ImageCopyService.Copy(source, targetDir, "photo.jpg", CopyConflict.Overwrite, null);

            Assert.Equal(CopyResult.Copied, result);
            Assert.Equal("new", File.ReadAllText(Path.Combine(targetDir, "photo.jpg")));
        }

        [Fact]
        public void Copy_skips_and_keeps_existing_when_conflict_policy_is_skip()
        {
            var source = CreateSource(content: "new");
            var targetDir = Path.Combine(root, "out");
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(targetDir, "photo.jpg"), "old");

            var result = ImageCopyService.Copy(source, targetDir, "photo.jpg", CopyConflict.Skip, null);

            Assert.Equal(CopyResult.Skipped, result);
            Assert.Equal("old", File.ReadAllText(Path.Combine(targetDir, "photo.jpg")));
        }

        [Fact]
        public void Copy_asks_resolver_on_conflict_and_honors_overwrite()
        {
            var source = CreateSource(content: "new");
            var targetDir = Path.Combine(root, "out");
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(targetDir, "photo.jpg"), "old");

            var asked = false;
            var result = ImageCopyService.Copy(source, targetDir, "photo.jpg", CopyConflict.Ask, _ =>
            {
                asked = true;
                return CopyDecision.Overwrite;
            });

            Assert.True(asked);
            Assert.Equal(CopyResult.Copied, result);
            Assert.Equal("new", File.ReadAllText(Path.Combine(targetDir, "photo.jpg")));
        }

        [Fact]
        public void Copy_returns_cancelled_when_resolver_cancels()
        {
            var source = CreateSource(content: "new");
            var targetDir = Path.Combine(root, "out");
            Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(targetDir, "photo.jpg"), "old");

            var result = ImageCopyService.Copy(source, targetDir, "photo.jpg", CopyConflict.Ask, _ => CopyDecision.Cancel);

            Assert.Equal(CopyResult.Cancelled, result);
            Assert.Equal("old", File.ReadAllText(Path.Combine(targetDir, "photo.jpg")));
        }

        [Fact]
        public void Copy_throws_when_source_is_missing()
        {
            Assert.Throws<FileNotFoundException>(
                () => ImageCopyService.Copy(Path.Combine(root, "missing.jpg"), Path.Combine(root, "out"), "x.jpg", CopyConflict.Skip, null));
        }
    }
}
