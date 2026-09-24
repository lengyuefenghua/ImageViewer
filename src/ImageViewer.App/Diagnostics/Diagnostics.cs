namespace ImageViewer.Core.Diagnostics
{
    // 进程级日志门面：默认空实现，宿主或测试可替换为真实 sink，无需改动大量埋点处的构造函数。
    public static class Diagnostics
    {
        private static readonly ILogSink NullSink = new NullLogSink();

        private static ILogSink _sink = NullSink;

        public static ILogSink Sink
        {
            get { return _sink; }
            // 收到 null 时回落到空实现，保证门面永远可用、调用点不必判空。
            set { _sink = value ?? NullSink; }
        }
    }
}
