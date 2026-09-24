using System;

namespace ImageViewer.Core.Diagnostics
{
    // Core 只依赖此抽象，不引用 NLog/WPF/SQLite 等具体日志实现。
    public interface ILogSink
    {
        void Log(LogSeverity severity, string logger, string message, Exception exception);
    }
}
