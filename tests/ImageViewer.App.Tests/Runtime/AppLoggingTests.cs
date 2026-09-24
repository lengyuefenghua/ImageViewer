using System;
using System.IO;
using ImageViewer.App.Runtime;
using ImageViewer.Core.Diagnostics;
using Xunit;

namespace ImageViewer.App.Tests.Runtime
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
        public void Initialize_replaces_the_diagnostics_sink_with_the_nlog_sink()
        {
            var directory = Path.Combine(Path.GetTempPath(), "idv-applogging-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var previous = Diagnostics.Sink;
            try
            {
                AppLogging.Initialize(directory, LogSeverity.Error);

                Assert.IsType<NLogLogSink>(Diagnostics.Sink);
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
    }
}
