using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using ImageViewer.Core.Diagnostics;

namespace ImageViewer.App.Standalone
{
    // 独立查看器模式的同目录扫描：Everything 唯一枚举规则的受控例外，只服务看图器的左右切换。
    public static class ViewerImageDirectoryScanner
    {
        private const string LoggerName = "ImageViewer";

        public static IReadOnlyList<string> Scan(string imagePath)
        {
            if (String.IsNullOrWhiteSpace(imagePath)) return new string[0];

            var images = new List<string>();
            string directory = null;
            try
            {
                directory = Path.GetDirectoryName(imagePath);
            }
            catch (ArgumentException error)
            {
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "查看器目录解析失败：" + imagePath, error);
            }

            if (!String.IsNullOrWhiteSpace(directory))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (StandaloneImageFiles.IsWhitelisted(file)) images.Add(file);
                    }

                    Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "查看器目录扫描完成：" + directory + "，图片 " + images.Count + " 张", null);
                }
                catch (Exception error) when (IsScanFailure(error))
                {
                    // 目录不可访问时降级为仅命令行图片：看图不能因此失败。
                    Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "查看器目录扫描失败，降级为仅命令行图片：" + directory, error);
                }
            }

            if (!images.Any(path => String.Equals(path, imagePath, StringComparison.OrdinalIgnoreCase)))
            {
                images.Add(imagePath);
            }

            // 资源管理器式自然排序：数字段按数值比较（1 < 2 < 10），大小写不敏感。
            images.Sort(CompareByFileName);
            return images;
        }

        private static int CompareByFileName(string left, string right)
        {
            return StrCmpLogicalW(Path.GetFileName(left), Path.GetFileName(right));
        }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string x, string y);

        private static bool IsScanFailure(Exception error)
        {
            return error is IOException
                || error is UnauthorizedAccessException
                || error is SecurityException
                || error is ArgumentException
                || error is NotSupportedException;
        }
    }
}
