using System;
using System.Drawing;

namespace ImageViewer.Imaging.Viewport
{
    public enum ViewportInitialZoom
    {
        Fit,
        ActualSize
    }

    public sealed class ViewportState
    {
        private double left;
        private double top;

        public ViewportState(
            int imageWidth,
            int imageHeight,
            int viewportWidth,
            int viewportHeight,
            ViewportInitialZoom initialZoom)
        {
            if (imageWidth <= 0) throw new ArgumentOutOfRangeException("imageWidth");
            if (imageHeight <= 0) throw new ArgumentOutOfRangeException("imageHeight");
            if (viewportWidth <= 0) throw new ArgumentOutOfRangeException("viewportWidth");
            if (viewportHeight <= 0) throw new ArgumentOutOfRangeException("viewportHeight");

            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            Zoom = initialZoom == ViewportInitialZoom.Fit
                ? Math.Min((double)viewportWidth / imageWidth, (double)viewportHeight / imageHeight)
                : 1.0;
            Zoom = Math.Max(0.01, Zoom);
            CenterImage();
        }

        public int ImageWidth { get; private set; }
        public int ImageHeight { get; private set; }
        public int ViewportWidth { get; private set; }
        public int ViewportHeight { get; private set; }
        public double Zoom { get; private set; }
        public double Left { get { return left; } }
        public double Top { get { return top; } }

        public Rectangle VisibleImageRectangle
        {
            get
            {
                var width = Math.Min(ImageWidth, Math.Max(1, (int)Math.Ceiling(ViewportWidth / Zoom)));
                var height = Math.Min(ImageHeight, Math.Max(1, (int)Math.Ceiling(ViewportHeight / Zoom)));
                var x = Clamp((int)Math.Round(left), 0, ImageWidth - width);
                var y = Clamp((int)Math.Round(top), 0, ImageHeight - height);
                return new Rectangle(x, y, width, height);
            }
        }

        public PointF ImagePointAt(PointF viewportPoint)
        {
            // 图像小于视口时渲染层居中显示，逆映射必须减去居中偏移（与窗口 ApplyTransform 对齐）。
            return new PointF(
                (float)(left + (viewportPoint.X - CenteringOffsetX()) / Zoom),
                (float)(top + (viewportPoint.Y - CenteringOffsetY()) / Zoom));
        }

        public void SetViewportSize(int viewportWidth, int viewportHeight)
        {
            if (viewportWidth <= 0) throw new ArgumentOutOfRangeException("viewportWidth");
            if (viewportHeight <= 0) throw new ArgumentOutOfRangeException("viewportHeight");

            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            ClampPosition();
        }

        public void ZoomAt(PointF viewportPoint, double zoom)
        {
            if (zoom <= 0) throw new ArgumentOutOfRangeException("zoom");
            var imagePoint = ImagePointAt(viewportPoint);
            Zoom = Math.Max(0.01, Math.Min(32.0, zoom));
            // 新缩放下的居中偏移参与重算，保持光标下的图像点不动。
            left = imagePoint.X - (viewportPoint.X - CenteringOffsetX()) / Zoom;
            top = imagePoint.Y - (viewportPoint.Y - CenteringOffsetY()) / Zoom;
            ClampPosition();
        }

        private double CenteringOffsetX()
        {
            return Math.Max(0, (ViewportWidth - ImageWidth * Zoom) / 2.0);
        }

        private double CenteringOffsetY()
        {
            return Math.Max(0, (ViewportHeight - ImageHeight * Zoom) / 2.0);
        }

        public void PanBy(PointF delta)
        {
            left += delta.X / Zoom;
            top += delta.Y / Zoom;
            ClampPosition();
        }

        private void CenterImage()
        {
            var visibleWidth = ViewportWidth / Zoom;
            var visibleHeight = ViewportHeight / Zoom;
            left = (ImageWidth - visibleWidth) / 2.0;
            top = (ImageHeight - visibleHeight) / 2.0;
            ClampPosition();
        }

        private void ClampPosition()
        {
            var visibleWidth = Math.Min(ImageWidth, ViewportWidth / Zoom);
            var visibleHeight = Math.Min(ImageHeight, ViewportHeight / Zoom);
            left = Math.Max(0, Math.Min(Math.Max(0, ImageWidth - visibleWidth), left));
            top = Math.Max(0, Math.Min(Math.Max(0, ImageHeight - visibleHeight), top));
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
