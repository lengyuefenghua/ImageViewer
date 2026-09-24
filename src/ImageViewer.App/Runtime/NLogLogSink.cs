using System;
using System.IO;
using System.Text;
using ImageViewer.Core.Diagnostics;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace ImageViewer.App.Runtime
{
    // NLog 实现：只允许出现在 App 项目，Core/下层通过 ILogSink 抽象使用。
    public sealed class NLogLogSink : ILogSink
    {
        private readonly string directory;
        private readonly LogFactory factory;

        public NLogLogSink(string directory, LogSeverity minimumLevel)
        {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("日志目录不能为空。", "directory");

            this.directory = directory;
            factory = new LogFactory();
            ApplyMinimumLevel(minimumLevel);
        }

        public void Log(LogSeverity severity, string logger, string message, Exception exception)
        {
            try
            {
                var logEvent = new LogEventInfo(MapLevel(severity), logger, message);
                if (exception != null) logEvent.Exception = exception;
                factory.GetLogger(logger).Log(logEvent);
            }
            catch
            {
                // 记录日志失败绝不中断主流程（spec：记录日志失败不影响主流程）。
            }
        }

        // 运行时重设最低级别并重新配置 LogFactory，使级别变更无需重启即时生效。
        public void SetMinimumLevel(LogSeverity severity)
        {
            try
            {
                ApplyMinimumLevel(severity);
            }
            catch
            {
                // 级别应用失败不影响主流程；保留上一份配置。
            }
        }

        private void ApplyMinimumLevel(LogSeverity severity)
        {
            // 每次重建配置与 target，避免复用已归属旧配置的 FileTarget 造成规则失效。
            var target = new FileTarget("daily-file")
            {
                // 本地日期文件名，与行内本地时间保持一致（不使用 universalTime）。
                FileName = Path.Combine(directory, "${shortdate}.log"),
                Layout = "${longdate}|${level}|${logger}|${message}${onexception:inner=|${exception:format=tostring}}",
                Encoding = Encoding.UTF8,
                KeepFileOpen = false,
                ConcurrentWrites = true
            };

            var configuration = new LoggingConfiguration();
            configuration.AddRule(MapLevel(severity), LogLevel.Fatal, target);
            factory.Configuration = configuration;
        }

        private static LogLevel MapLevel(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Debug: return LogLevel.Debug;
                case LogSeverity.Info: return LogLevel.Info;
                case LogSeverity.Warn: return LogLevel.Warn;
                case LogSeverity.Error: return LogLevel.Error;
                case LogSeverity.Fatal: return LogLevel.Fatal;
                default: return LogLevel.Error;
            }
        }
    }
}
