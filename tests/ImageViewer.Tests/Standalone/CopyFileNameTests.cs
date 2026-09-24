using System;
using ImageViewer.Standalone;
using Xunit;

namespace ImageViewer.Tests.Standalone
{
    public sealed class CopyFileNameTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 25, 12, 0, 0);

        [Fact]
        public void Build_keeps_original_name_when_no_timestamp_and_no_suffix()
        {
            Assert.Equal("photo.jpg", CopyFileName.Build("photo.jpg", null, "", Now));
            Assert.Equal("photo.jpg", CopyFileName.Build("photo.jpg", "  ", "  ", Now));
        }

        [Fact]
        public void Build_appends_timestamp_before_extension()
        {
            Assert.Equal("photo_20260925_120000.jpg", CopyFileName.Build("photo.jpg", null, "yyyyMMdd_HHmmss", Now));
        }

        [Fact]
        public void Build_appends_suffix_only()
        {
            Assert.Equal("photo_x.jpg", CopyFileName.Build("photo.jpg", "_x", "", Now));
        }

        [Fact]
        public void Build_appends_suffix_after_timestamp()
        {
            Assert.Equal("photo_20260925_120000_x.jpg", CopyFileName.Build("photo.jpg", "_x", "yyyyMMdd_HHmmss", Now));
        }

        [Fact]
        public void Build_ignores_invalid_timestamp_format()
        {
            Assert.Equal("photo.jpg", CopyFileName.Build("photo.jpg", null, "yyyy-MM-dd 'unterminated", Now));
        }

        [Fact]
        public void Build_handles_name_without_extension()
        {
            Assert.Equal("photo_20260925_120000", CopyFileName.Build("photo", null, "yyyyMMdd_HHmmss", Now));
        }
    }
}
