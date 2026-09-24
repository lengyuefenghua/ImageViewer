using System;
using System.Drawing;
using System.IO;
using ImageViewer.Imaging.Viewport;

namespace ImageViewer.Imaging.Wic
{
    public sealed class JpegViewportDecoder
    {
        private const long MaximumDecodedBytes = 512L * 1024 * 1024;

        public Bitmap Decode(string path, Rectangle sourceRectangle)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentException("路径不能为空。", "path");
            if (sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0) throw new ArgumentOutOfRangeException("sourceRectangle");

            var size = ReadSize(path);
            EnsureWithinBudget(size);
            using (var source = new Bitmap(path))
            {
                var image = new Rectangle(Point.Empty, source.Size);
                var clipped = Rectangle.Intersect(sourceRectangle, image);
                if (clipped.Width <= 0 || clipped.Height <= 0) throw new ArgumentOutOfRangeException("sourceRectangle");
                return source.Clone(clipped, source.PixelFormat);
            }
        }

        private static Size ReadSize(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (ReadByte(stream) != 0xff || ReadByte(stream) != 0xd8)
                    throw new InvalidDataException("不是有效的 JPEG 文件。");

                while (true)
                {
                    var marker = ReadMarker(stream);
                    if (marker == 0xd9 || marker == 0xda) throw new InvalidDataException("JPEG 缺少尺寸信息。");
                    if (marker >= 0xd0 && marker <= 0xd7) continue;

                    var length = ReadUInt16(stream);
                    if (length < 2) throw new InvalidDataException("JPEG 段长度无效。");
                    if (IsStartOfFrame(marker))
                    {
                        ReadByte(stream);
                        var height = ReadUInt16(stream);
                        var width = ReadUInt16(stream);
                        if (width <= 0 || height <= 0) throw new InvalidDataException("JPEG 尺寸无效。");
                        return new Size(width, height);
                    }

                    stream.Seek(length - 2, SeekOrigin.Current);
                }
            }
        }

        private static bool IsStartOfFrame(int marker)
        {
            return marker >= 0xc0 && marker <= 0xc3
                || marker >= 0xc5 && marker <= 0xc7
                || marker >= 0xc9 && marker <= 0xcb
                || marker >= 0xcd && marker <= 0xcf;
        }

        private static int ReadMarker(Stream stream)
        {
            int value;
            do { value = ReadByte(stream); } while (value == 0xff);
            if (value < 0) throw new InvalidDataException("JPEG 标记不完整。");
            return value;
        }

        private static int ReadUInt16(Stream stream)
        {
            var high = ReadByte(stream);
            var low = ReadByte(stream);
            if (high < 0 || low < 0) throw new InvalidDataException("JPEG 段不完整。");
            return (high << 8) | low;
        }

        private static int ReadByte(Stream stream)
        {
            return stream.ReadByte();
        }

        private static void EnsureWithinBudget(Size size)
        {
            if ((long)size.Width > MaximumDecodedBytes / 4 / size.Height)
                throw new JpegDecodeBudgetExceededException("JPEG 精确视口解码超过 512 MB 内存预算。");
        }
    }

    public sealed class JpegDecodeBudgetExceededException : InvalidOperationException
    {
        public JpegDecodeBudgetExceededException(string message) : base(message) { }
    }
}
