using System;
using System.IO;

namespace ImageViewer.App.Standalone
{
    // 独立查看器模式共享的图片白名单判定：与主程序枚举白名单保持一致（jpg/png/bmp）。
    internal static class StandaloneImageFiles
    {
        private static readonly string[] WhitelistedExtensions = { ".jpg", ".png", ".bmp" };

        public static bool IsWhitelisted(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;

            string extension;
            try
            {
                // .NET Framework 的 GetExtension 对含非法字符的路径会抛 ArgumentException，按不匹配处理。
                extension = Path.GetExtension(path);
            }
            catch (ArgumentException)
            {
                return false;
            }

            foreach (var whitelisted in WhitelistedExtensions)
            {
                if (String.Equals(extension, whitelisted, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
