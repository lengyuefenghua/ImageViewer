using System;
using System.Collections.Generic;
using System.IO;

namespace ImageViewer.Viewer
{
    // 命令行图片参数解析：只接受存在且扩展名属于白名单的图片路径，供启动模式判定。
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
                if (!ImageFileWhitelist.IsWhitelisted(candidate)) continue;
                if (!File.Exists(candidate)) continue;

                path = Path.GetFullPath(candidate);
                return true;
            }

            return false;
        }

        // 解析所有有效图片路径（保持命令行顺序）；用于多选/搜索结果一次性打开后的连续浏览。
        public static bool TryResolveAll(string[] args, out IReadOnlyList<string> paths)
        {
            var resolved = new List<string>();
            if (args != null)
            {
                foreach (var argument in args)
                {
                    if (String.IsNullOrWhiteSpace(argument)) continue;

                    var candidate = argument.Trim();
                    if (!ImageFileWhitelist.IsWhitelisted(candidate)) continue;
                    if (!File.Exists(candidate)) continue;

                    resolved.Add(Path.GetFullPath(candidate));
                }
            }

            paths = resolved;
            return resolved.Count > 0;
        }
    }
}
