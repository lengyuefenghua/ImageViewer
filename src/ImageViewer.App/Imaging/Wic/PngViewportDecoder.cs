using System;
using System.Drawing;
using System.IO;
using ImageViewer.Imaging.Viewport;

namespace ImageViewer.Imaging.Wic
{
    public sealed class PngViewportDecoder
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
                return source.Clone(Clip(sourceRectangle, source.Size), source.PixelFormat);
            }
        }

        public void EnsureWithinBudget(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentException("路径不能为空。", "path");
            EnsureWithinBudget(ReadSize(path));
        }

        private static Size ReadSize(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                var signature = reader.ReadBytes(8);
                if (signature.Length != 8 || signature[0] != 137 || signature[1] != 80 || signature[2] != 78 || signature[3] != 71)
                {
                    throw new InvalidDataException("不是有效的 PNG 文件。");
                }

                reader.ReadBytes(8);
                var width = ReadBigEndianInt32(reader.ReadBytes(4));
                var height = ReadBigEndianInt32(reader.ReadBytes(4));
                if (width <= 0 || height <= 0) throw new InvalidDataException("PNG 尺寸无效。");
                return new Size(width, height);
            }
        }

        private static int ReadBigEndianInt32(byte[] bytes)
        {
            if (bytes.Length != 4) throw new InvalidDataException("PNG 头部不完整。");
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        private static void EnsureWithinBudget(Size size)
        {
            if ((long)size.Width > MaximumDecodedBytes / 4 / size.Height)
            {
                throw new PngDecodeBudgetExceededException("PNG 解码内存预算为 512 MB。");
            }
        }

        private static Rectangle Clip(Rectangle requested, Size imageSize)
        {
            var image = new Rectangle(Point.Empty, imageSize);
            var clipped = Rectangle.Intersect(requested, image);
            if (clipped.Width <= 0 || clipped.Height <= 0) throw new ArgumentOutOfRangeException("sourceRectangle");
            return clipped;
        }
    }
}
