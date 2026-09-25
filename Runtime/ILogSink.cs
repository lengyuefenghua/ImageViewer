using System;

namespace ImageViewer.Runtime
{
    // Core 只依赖此抽象，不引用具体日志实现（WPF、文件系统等）。
    public interface ILogSink
    {
        void Log(LogSeverity severity, string logger, string message, Exception exception);
    }
}
