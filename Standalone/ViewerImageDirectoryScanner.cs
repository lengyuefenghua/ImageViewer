using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using ImageViewer.Core.Diagnostics;

namespace ImageViewer.Standalone
{
    // 独立查看器模式的同目录扫描：Everything 唯一枚举规则的受控例外，只服务看图器的左右切换。
    public static class ViewerImageDirectoryScanner
    {
        private const string LoggerName = "ImageViewer";

        public static IReadOnlyList<string> Scan(string imagePath)
        {
            if (String.IsNullOrWhiteSpace(imagePath)) return new string[0];

            string directory = null;
            try
            {
                directory = Path.GetDirectoryName(imagePath);
            }
            catch (ArgumentException error)
            {
                Diagnostics.Sink.Log(LogSeverity.Debug, LoggerName, "查看器目录解析失败：" + imagePath, error);
            }

            var images = String.IsNullOrWhiteSpace(directory)
                ? new List<string>()
                : new List<string>(ScanDirectory(directory));

            if (!images.Any(path => String.Equals(path, imagePath, StringComparison.OrdinalIgnoreCase)))
            {
                images.Add(imagePath);
            }

            // 资源管理器式自然排序：数字段按数值比较（1 < 2 < 10），大小写不敏感。
            images.Sort(CompareByFileName);
            return images;
        }

        // 只列出某目录下的白名单图片（不递归、不追加任何种子文件），自然排序。
        public static IReadOnlyList<string> ScanDirectory(string directory)
        {
            var images = new List<string>();
            if (String.IsNullOrWhiteSpace(directory)) return images;

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
                // 目录不可访问时返回空：看图不能因此失败。
                Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "查看器目录扫描失败：" + directory, error);
            }

            images.Sort(CompareByFileName);
            return images;
        }

        // 同级目录（含自身）自然排序，供看图器跨目录连续浏览；父目录不可枚举时仅返回自身。
        public static IReadOnlyList<string> EnumerateSiblingDirectories(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) return new string[0];

            var normalized = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string parent;
            try
            {
                parent = Path.GetDirectoryName(normalized);
            }
            catch (ArgumentException)
            {
                return new string[0];
            }

            var directories = new List<string>();
            if (!String.IsNullOrWhiteSpace(parent))
            {
                try
                {
                    directories.AddRange(Directory.GetDirectories(parent));
                }
                catch (Exception error) when (IsScanFailure(error))
                {
                    Diagnostics.Sink.Log(LogSeverity.Warn, LoggerName, "同级目录枚举失败：" + parent, error);
                }
            }

            if (!directories.Any(candidate => String.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                directories.Add(normalized);
            }

            directories.Sort(CompareByDirectoryName);
            return directories;
        }

        private static int CompareByDirectoryName(string left, string right)
        {
            return StrCmpLogicalW(Path.GetFileName(left), Path.GetFileName(right));
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
