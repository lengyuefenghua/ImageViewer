using System;
using ImageViewer.Services;
using Xunit;

namespace ImageViewer.Tests.Services
{
    public sealed class UserChoiceHashTests
    {
        private const string Sid = "s-1-5-21-1398101749-1678756719-1402360515-1001";
        private const string Experience = "User Choice set via Windows User Experience {D18B6DD5-6124-4341-9318-804003BAFA0B}";
        // 真机用 PS-SFTA 写入 .jpg 时产生的 Hash 输入（时间为 UTC 2026-11-04 04:13）。
        private const string BaseInfo = ".jpgs-1-5-21-1398101749-1678756719-1402360515-1001imageviewer.image01dd6c12c74e6e00user choice set via windows user experience {d18b6dd5-6124-4341-9318-804003bafa0b}";

        [Fact]
        public void BuildBaseInfo_truncates_to_minute_and_lowercases()
        {
            var baseInfo = UserChoiceHash.BuildBaseInfo(
                ".jpg",
                Sid,
                "ImageViewer.Image",
                new DateTime(2026, 11, 4, 4, 13, 42, DateTimeKind.Utc),
                Experience);

            Assert.Equal(BaseInfo, baseInfo);
        }

        [Fact]
        public void Compute_matches_the_hash_windows_accepted()
        {
            Assert.Equal("eg1nwCkvu5U=", UserChoiceHash.Compute(BaseInfo));
        }
    }
}
