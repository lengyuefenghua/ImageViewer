using System;
using System.IO;

namespace ImageViewer.App.Standalone
{
    // 命令行图片参数解析：只接受存在且扩展名属于白名单的单个图片路径，供独立查看器模式判定。
    public static class CommandLineImageArgument
    {
        public static bool TryResolve(string[] args, out string path)
        {
            path = null;
            if (args == null) return false;

            foreach (var argument in args)
            {
                if (String.IsNullOrWhiteSpace(argument)) continue;

                var candidate = argument.Trim();
                if (!StandaloneImageFiles.IsWhitelisted(candidate)) continue;
                if (!File.Exists(candidate)) continue;

                path = Path.GetFullPath(candidate);
                return true;
            }

            return false;
        }
    }
}
