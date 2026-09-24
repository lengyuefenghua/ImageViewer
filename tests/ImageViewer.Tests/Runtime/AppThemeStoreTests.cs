using System;
using System.Collections.Generic;
using ImageViewer.Runtime;
using Xunit;

namespace ImageViewer.Tests.Runtime
{
    public sealed class AppThemeStoreTests
    {
        [Fact]
        public void Resolve_defaults_to_dark_when_missing_or_invalid()
        {
            Assert.Equal(AppTheme.Dark, AppThemeStore.Resolve(new Dictionary<string, string>()));
            Assert.Equal(AppTheme.Dark, AppThemeStore.Resolve(null));
            Assert.Equal(AppTheme.Dark, AppThemeStore.Resolve(new Dictionary<string, string> { { "Appearance.Theme", "neon" } }));
        }

        [Theory]
        [InlineData("Dark", AppTheme.Dark)]
        [InlineData("light", AppTheme.Light)]
        [InlineData("SYSTEM", AppTheme.System)]
        public void Resolve_parses_configured_theme_case_insensitively(string configured, AppTheme expected)
        {
            var settings = new Dictionary<string, string> { { "Appearance.Theme", configured } };

            Assert.Equal(expected, AppThemeStore.Resolve(settings));
        }
    }
}
