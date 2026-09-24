using System;
using System.Drawing;
using System.IO;
using ImageViewer.Imaging.Wic;

namespace ImageViewer.Imaging.Viewport
{
    public sealed class ViewportImageDecoder
    {
        public void EnsureWithinBudget(string path)
        {
            var extension = Path.GetExtension(path);
            if (String.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)) new PngViewportDecoder().EnsureWithinBudget(path);
        }

        public Bitmap Decode(string path, Rectangle sourceRectangle)
        {
            var extension = Path.GetExtension(path);
            return String.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                ? new PngViewportDecoder().Decode(path, sourceRectangle)
                : new JpegViewportDecoder().Decode(path, sourceRectangle);
        }
    }
}
