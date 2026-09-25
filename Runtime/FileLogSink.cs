using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ImageViewer.Runtime
{
    // 轻量文件日志 sink：每日一个 yyyy-MM-dd.log，行格式 时间|级别|logger|message（有异常时追加异常详情）；
    // 仅依赖 System.IO，不引入日志框架，且写日志失败绝不中断主流程。
    public sealed class FileLogSink : ILogSink
    {
        private readonly string directory;
        private readonly object writeLock = new object();
        private LogSeverity minimumLevel;

        public FileLogSink(string directory, LogSeverity minimumLevel)
        {
            if (String.IsNullOrWhiteSpace(directory)) throw new ArgumentException("日志目录不能为空。", "directory");

            this.directory = directory;
            this.minimumLevel = minimumLevel;
        }

        public void Log(LogSeverity severity, string logger, string message, Exception exception)
        {
            if (severity < minimumLevel) return;

            try
            {
                var line = Format(severity, logger, message, exception);
                var path = Path.Combine(directory, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");

                // 多线程（UI + 后台解码）可能并发写同一文件，锁定后整行追加，避免交叉写坏。
                lock (writeLock)
                {
                    Directory.CreateDirectory(directory);
                    File.AppendAllText(path, line + Environment.NewLine, NoBomUtf8);
                }
            }
            catch
            {
                // 记录日志失败绝不中断主流程（spec：记录日志失败不影响主流程）。
            }
        }

        // 运行时重设最低级别，无需重启即时生效。
        public void SetMinimumLevel(LogSeverity severity)
        {
            minimumLevel = severity;
        }

        private static readonly UTF8Encoding NoBomUtf8 = new UTF8Encoding(false);

        private static string Format(LogSeverity severity, string logger, string message, Exception exception)
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff", CultureInfo.InvariantCulture));
            builder.Append('|').Append(severity);
            builder.Append('|').Append(logger);
            builder.Append('|').Append(message);
            if (exception != null)
            {
                builder.Append('|').Append(exception);
            }
            return builder.ToString();
        }
    }
}
