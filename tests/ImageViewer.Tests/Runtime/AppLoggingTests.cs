using System;
using System.Collections.Generic;
using System.IO;
using ImageViewer.Runtime;
using Xunit;

namespace ImageViewer.Tests.Runtime
{
    public sealed class AppLoggingTests
    {
        [Fact]
        public void SetMinimumLevel_before_initialize_does_not_throw()
        {
            // 未初始化时必须是 no-op：启动早期或测试环境调用不应抛异常。
            AppLogging.SetMinimumLevel(LogSeverity.Debug);
        }

        [Fact]
        public void Initialize_replaces_the_diagnostics_sink_with_the_file_sink()
        {
            var directory = Path.Combine(Path.GetTempPath(), "idv-applogging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var previous = Diagnostics.Sink;
            try
            {
                AppLogging.Initialize(directory, LogSeverity.Error);

                Assert.IsType<FileLogSink>(Diagnostics.Sink);
            }
            finally
            {
                // 恢复进程级静态 sink，避免污染同进程内的其它测试。
                Diagnostics.Sink = previous;
                try
                {
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
                catch (IOException)
                {
                }
            }
        }

        [Fact]
        public void ResolveMinimumLevel_returns_error_when_key_is_missing()
        {
            Assert.Equal(LogSeverity.Error, AppLogging.ResolveMinimumLevel(new Dictionary<string, string>()));
            Assert.Equal(LogSeverity.Error, AppLogging.ResolveMinimumLevel(null));
        }

        [Theory]
        [InlineData("Debug", LogSeverity.Debug)]
        [InlineData("Info", LogSeverity.Info)]
        [InlineData("warn", LogSeverity.Warn)]
        [InlineData("ERROR", LogSeverity.Error)]
        [InlineData("Fatal", LogSeverity.Fatal)]
        public void ResolveMinimumLevel_parses_configured_level_case_insensitively(string configured, LogSeverity expected)
        {
            var settings = new Dictionary<string, string> { { AppLogging.MinimumLevelKey, configured } };

            Assert.Equal(expected, AppLogging.ResolveMinimumLevel(settings));
        }

        [Fact]
        public void ResolveMinimumLevel_returns_error_when_value_is_invalid()
        {
            var settings = new Dictionary<string, string> { { AppLogging.MinimumLevelKey, "chatty" } };

            Assert.Equal(LogSeverity.Error, AppLogging.ResolveMinimumLevel(settings));
        }
    }
}
