using System;
using System.IO;

namespace ImageViewer.Services
{
    // 图片格式白名单（jpg/png/bmp）。
    internal static class ImageFileWhitelist
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
