using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ImageViewer.Viewer
{
    public static class CopyFileName
    {
        // 生成复制后的文件名：主干 +（时间戳? "_"+格式）+ 后缀（原样）+ 扩展名；格式非法则不追加时间戳。
        public static string Build(string sourceFileName, string suffix, string timestampFormat, DateTime now)
        {
            if (String.IsNullOrWhiteSpace(sourceFileName)) return sourceFileName;

            var extension = Path.GetExtension(sourceFileName);
            var stem = Path.GetFileNameWithoutExtension(sourceFileName);
            var builder = new StringBuilder(stem);

            if (!String.IsNullOrWhiteSpace(timestampFormat))
            {
                string stamp;
                try
                {
                    stamp = now.ToString(timestampFormat, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    stamp = null;
                }
                if (!String.IsNullOrWhiteSpace(stamp)) builder.Append('_').Append(stamp);
            }

            if (!String.IsNullOrWhiteSpace(suffix)) builder.Append(suffix);
            return builder.Append(extension).ToString();
        }
    }
}
