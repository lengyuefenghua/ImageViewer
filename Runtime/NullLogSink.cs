using System;

namespace ImageViewer.Runtime
{
    // 空实现：吞掉日志调用，保证记录日志本身失败时绝不中断主流程。
    public sealed class NullLogSink : ILogSink
    {
        public void Log(LogSeverity severity, string logger, string message, Exception exception)
        {
            // 有意不做任何事，也绝不抛出异常。
        }
    }
}
