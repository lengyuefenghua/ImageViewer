using System;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageViewer.Services
{
    public static class BitmapSourceLoader
    {
        // 只读且允许外部改名/删除/移动：加载期间也不阻止其他程序操作该文件。
        private static FileStream OpenReadShared(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        public static object Load(string path, int maximumWidth = 0, int maximumHeight = 0)
        {
            var bitmap = new BitmapImage();
            using (var stream = OpenReadShared(path))
            {
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (maximumWidth > 0) bitmap.DecodePixelWidth = maximumWidth;
                if (maximumHeight > 0) bitmap.DecodePixelHeight = maximumHeight;
                bitmap.StreamSource = stream; bitmap.EndInit();
            }
            if (bitmap.CanFreeze) bitmap.Freeze();
            return bitmap;
        }

        public static object LoadForDisplay(string path, int imageWidth, int imageHeight, long maxDecodedBytes = 1610612736L)
        {
            var bitmap = new BitmapImage();
            using (var stream = OpenReadShared(path))
            {
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                var decodedBytes = (long)imageWidth * imageHeight * 4;
                if (imageWidth > 0 && imageHeight > 0 && decodedBytes > maxDecodedBytes)
                {
                    var scale = Math.Sqrt(maxDecodedBytes / (double)decodedBytes);
                    bitmap.DecodePixelWidth = Math.Max(1, (int)Math.Round(imageWidth * scale));
                }
                bitmap.StreamSource = stream; bitmap.EndInit();
            }
            if (bitmap.CanFreeze) bitmap.Freeze();
            return bitmap;
        }

        public static System.Windows.Size ReadDimensions(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("路径不能为空。", "path");
            using (var stream = OpenReadShared(path))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                return new System.Windows.Size(frame.PixelWidth, frame.PixelHeight);
            }
        }

        public static object FromBitmap(Bitmap bitmap)
        {
            if (bitmap == null) throw new ArgumentNullException("bitmap");
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); stream.Position = 0;
                var source = new BitmapImage();
                source.BeginInit(); source.CacheOption = BitmapCacheOption.OnLoad; source.StreamSource = stream; source.EndInit();
                if (source.CanFreeze) source.Freeze();
                return source;
            }
        }

        public static int PixelCount(object bitmap)
        {
            var source = bitmap as BitmapSource;
            if (source == null) throw new ArgumentException("位图类型无效。", "bitmap");
            return checked(source.PixelWidth * source.PixelHeight);
        }

        public static void Freeze(object bitmap)
        {
            var source = bitmap as BitmapSource;
            if (source == null) throw new ArgumentException("位图类型无效。", "bitmap");
            if (source.CanFreeze) source.Freeze();
        }
    }
}
