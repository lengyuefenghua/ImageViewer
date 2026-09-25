using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace ImageViewer.Runtime
{
    // App 侧日志入口：进程级静态持有文件 sink，供启动接线、级别切换与全局异常注册使用。
    public static class AppLogging
    {
        private const string LoggerName = "ImageViewer";

        // appSettings 中最低日志级别的键；非法/缺失时回落 Error。
        public const string MinimumLevelKey = "Logging.MinimumLevel";

        private static FileLogSink sink;
        private static bool domainHandlersRegistered;
        private static bool dispatcherHandlerRegistered;

        public static void Initialize(string directory, LogSeverity minimumLevel)
        {
            sink = new FileLogSink(directory, minimumLevel);
            Diagnostics.Sink = sink;
        }

        // 从 appSettings 解析最低日志级别；缺失或非法值回落 Error。
        public static LogSeverity ResolveMinimumLevel(IDictionary<string, string> settings)
        {
            string value;
            LogSeverity parsed;
            if (settings != null
                && settings.TryGetValue(MinimumLevelKey, out value)
                && Enum.TryParse(value, true, out parsed)
                && Enum.IsDefined(typeof(LogSeverity), parsed))
            {
                return parsed;
            }
            return LogSeverity.Error;
        }

        // 未初始化时为 no-op：启动早期或测试环境调用不应抛异常。
        public static void SetMinimumLevel(LogSeverity severity)
        {
            var current = sink;
            if (current == null) return;
            current.SetMinimumLevel(severity);
        }

        // 幂等：各处理器只注册一次，避免重复处理导致日志重复。
        // Dispatcher 处理器单独用标志，保证 Application.Current 尚未就绪时后续调用仍能补注册。
        public static void RegisterGlobalExceptionHandlers()
        {
            if (!domainHandlersRegistered)
            {
                domainHandlersRegistered = true;

                AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                    Diagnostics.Sink.Log(LogSeverity.Fatal, LoggerName, "未处理异常", args.ExceptionObject as Exception);

                TaskScheduler.UnobservedTaskException += (sender, args) =>
                    // 不调用 SetObserved，保持未观察任务异常的原有行为。
                    Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "未观察的任务异常", args.Exception);
            }

            if (!dispatcherHandlerRegistered && Application.Current != null)
            {
                Application.Current.DispatcherUnhandledException += (sender, args) =>
                {
                    // 只记录，不设置 args.Handled，保持 WPF 原有异常处理行为。
                    Diagnostics.Sink.Log(LogSeverity.Error, LoggerName, "UI 线程未处理异常", args.Exception);
                };
                dispatcherHandlerRegistered = true;
            }
        }
    }
}
