using System;
using System.IO;
using System.Threading;
using ImageViewer.App.Runtime;
using ImageViewer.Core.Diagnostics;
using Xunit;

namespace ImageViewer.App.Tests.Runtime
{
    public sealed class NLogLogSinkTests : IDisposable
    {
        private readonly string directory;

        public NLogLogSinkTests()
        {
            directory = Path.Combine(Path.GetTempPath(), "idv-nlog-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void Log_writes_error_line_to_local_date_file_with_level_logger_and_message()
        {
            var sink = new NLogLogSink(directory, LogSeverity.Debug);

            sink.Log(LogSeverity.Error, "Test", "hello", null);

            // ReadLogContaining 只在本地日期文件名 yyyy-MM-dd.log 存在时才读取，即同时验证了文件名规则。
            var content = ReadLogContaining("|Error|Test|hello");
            Assert.Contains("|Error|Test|hello", content);
        }

        [Fact]
        public void Log_appends_exception_details_when_exception_is_present()
        {
            var sink = new NLogLogSink(directory, LogSeverity.Debug);

            sink.Log(LogSeverity.Error, "Test", "failed", new InvalidOperationException("boom-marker"));

            var content = ReadLogContaining("boom-marker");
            Assert.Contains("boom-marker", content);
        }

        [Fact]
        public void SetMinimumLevel_filters_lower_severity_and_keeps_higher_severity()
        {
            var sink = new NLogLogSink(directory, LogSeverity.Debug);
            sink.SetMinimumLevel(LogSeverity.Error);

            sink.Log(LogSeverity.Info, "Test", "info-should-be-filtered", null);
            sink.Log(LogSeverity.Error, "Test", "error-should-be-written", null);

            var content = ReadLogContaining("error-should-be-written");
            Assert.DoesNotContain("info-should-be-filtered", content);
        }

        private string CurrentLogFilePath
        {
            get { return Path.Combine(directory, DateTime.Now.ToString("yyyy-MM-dd") + ".log"); }
        }

        // NLog FileTarget 在 KeepFileOpen=false 时每次写入后关闭文件；这里做带总超时的条件轮询，避免磁盘延迟导致偶发失败。
        // 采用短退避（10ms 起、上限 50ms），替代固定 50ms×100：日志通常很快落盘，能更早命中并减少等待。
        private string ReadLogContaining(string expectedFragment)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            var delayMilliseconds = 10;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(CurrentLogFilePath))
                {
                    try
                    {
                        var content = File.ReadAllText(CurrentLogFilePath);
                        if (content.Contains(expectedFragment)) return content;
                    }
                    catch (IOException)
                    {
                    }
                }

                Thread.Sleep(delayMilliseconds);
                delayMilliseconds = Math.Min(50, delayMilliseconds + 10);
            }

            Assert.True(File.Exists(CurrentLogFilePath), "当日日志文件未生成: " + CurrentLogFilePath);
            return File.ReadAllText(CurrentLogFilePath);
        }
    }
}
