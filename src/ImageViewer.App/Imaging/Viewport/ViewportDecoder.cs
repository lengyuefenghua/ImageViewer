using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace ImageViewer.Imaging.Viewport
{
    public sealed class ViewportRequest
    {
        public ViewportRequest(Rectangle sourceRectangle)
        {
            SourceRectangle = sourceRectangle;
        }

        public Rectangle SourceRectangle { get; private set; }
    }

    public sealed class ViewportRenderRequest
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        internal ViewportRenderRequest(string imageKey, ViewportRequest viewport)
        {
            ImageKey = imageKey;
            Viewport = viewport;
        }

        public string ImageKey { get; private set; }
        public ViewportRequest Viewport { get; private set; }
        public CancellationToken CancellationToken { get { return cancellation.Token; } }
        internal void Cancel() { cancellation.Cancel(); }
    }

    public sealed class PngDecodeBudgetExceededException : InvalidOperationException
    {
        public PngDecodeBudgetExceededException(string message) : base(message) { }
    }

    public sealed class ViewportDecoder
    {
        private const int MaximumCachedBlocks = 3;
        private const long DefaultMaximumCachedBytes = 64L * 1024 * 1024;
        private readonly LinkedList<CacheEntry> cache = new LinkedList<CacheEntry>();
        private readonly long maximumCachedBytes;

        public ViewportDecoder(long maximumCachedBytes = DefaultMaximumCachedBytes)
        {
            if (maximumCachedBytes <= 0) throw new ArgumentOutOfRangeException("maximumCachedBytes");
            this.maximumCachedBytes = maximumCachedBytes;
        }

        public int CachedBlockCount { get { return cache.Count; } }
        public long CachedByteCount { get; private set; }

        public ViewportRequest GetRequestAfterIdle(
            ViewportState state,
            DateTimeOffset transformedAt,
            DateTimeOffset now)
        {
            if (state == null) throw new ArgumentNullException("state");
            if (now - transformedAt < TimeSpan.FromMilliseconds(80)) return null;

            var visible = state.VisibleImageRectangle;
            var horizontal = (int)Math.Ceiling(visible.Width * 0.2);
            var vertical = (int)Math.Ceiling(visible.Height * 0.2);
            var left = Math.Max(0, visible.Left - horizontal);
            var top = Math.Max(0, visible.Top - vertical);
            var right = Math.Min(state.ImageWidth, visible.Right + horizontal);
            var bottom = Math.Min(state.ImageHeight, visible.Bottom + vertical);
            return new ViewportRequest(Rectangle.FromLTRB(left, top, right, bottom));
        }

        public void CacheBlock(string imageKey, Rectangle sourceRectangle, byte[] pixels)
        {
            if (imageKey == null) throw new ArgumentNullException("imageKey");
            if (pixels == null) throw new ArgumentNullException("pixels");
            if (pixels.LongLength > maximumCachedBytes) return;
            var existing = Find(imageKey, sourceRectangle);
            if (existing != null)
            {
                cache.Remove(existing);
                cache.AddFirst(existing);
                return;
            }

            cache.AddFirst(new CacheEntry(imageKey, sourceRectangle, pixels));
            CachedByteCount += pixels.LongLength;
            while (cache.Count > MaximumCachedBlocks || CachedByteCount > maximumCachedBytes)
            {
                var last = cache.Last;
                CachedByteCount -= last.Value.Pixels.LongLength;
                cache.RemoveLast();
            }
        }

        public bool TryGetCachedBlock(string imageKey, Rectangle sourceRectangle, out byte[] pixels)
        {
            var entry = Find(imageKey, sourceRectangle);
            if (entry == null)
            {
                pixels = null;
                return false;
            }

            pixels = entry.Value.Pixels;
            cache.Remove(entry);
            cache.AddFirst(entry);
            return true;
        }

        public void EnsurePngWithinBudget(long decodedBytes)
        {
            if (decodedBytes < 0) throw new ArgumentOutOfRangeException("decodedBytes");
            const long budget = 512L * 1024 * 1024;
            if (decodedBytes > budget)
            {
                throw new PngDecodeBudgetExceededException("PNG 解码内存预算为 512 MB。");
            }
        }

        private LinkedListNode<CacheEntry> Find(string imageKey, Rectangle sourceRectangle)
        {
            for (var node = cache.First; node != null; node = node.Next)
            {
                if (String.Equals(node.Value.ImageKey, imageKey, StringComparison.Ordinal)
                    && node.Value.SourceRectangle == sourceRectangle)
                {
                    return node;
                }
            }
            return null;
        }

        private sealed class CacheEntry
        {
            public CacheEntry(string imageKey, Rectangle sourceRectangle, byte[] pixels)
            {
                ImageKey = imageKey;
                SourceRectangle = sourceRectangle;
                Pixels = pixels;
            }

            public string ImageKey { get; private set; }
            public Rectangle SourceRectangle { get; private set; }
            public byte[] Pixels { get; set; }
        }
    }
}
