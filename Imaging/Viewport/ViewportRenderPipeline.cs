using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace ImageViewer.Imaging.Viewport
{
    public sealed class ViewportPixelBlock
    {
        public ViewportPixelBlock(Rectangle sourceRectangle, byte[] pixels)
        {
            if (sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0) throw new ArgumentOutOfRangeException("sourceRectangle");
            if (pixels == null) throw new ArgumentNullException("pixels");
            SourceRectangle = sourceRectangle;
            Pixels = pixels;
        }

        public Rectangle SourceRectangle { get; private set; }
        public byte[] Pixels { get; private set; }
    }

    public sealed class ViewportRenderPipeline
    {
        private const int MaximumCachedBlocks = 3;
        private readonly long maximumCachedBytes;
        private readonly LinkedList<CacheEntry> cache = new LinkedList<CacheEntry>();
        private ViewportRenderRequest currentRequest;

        public ViewportRenderPipeline(long maximumCachedBytes = 64L * 1024 * 1024)
        {
            if (maximumCachedBytes <= 0) throw new ArgumentOutOfRangeException("maximumCachedBytes");
            this.maximumCachedBytes = maximumCachedBytes;
        }

        public string CurrentImageKey { get; private set; }
        public int CachedBlockCount { get { return cache.Count; } }
        public long CachedByteCount { get; private set; }

        public ViewportRenderRequest Begin(string imageKey, ViewportRequest request)
        {
            if (String.IsNullOrEmpty(imageKey)) throw new ArgumentException("图片标识不能为空。", "imageKey");
            if (request == null) throw new ArgumentNullException("request");
            if (currentRequest != null) currentRequest.Cancel();
            CurrentImageKey = imageKey;
            currentRequest = new ViewportRenderRequest(imageKey, request);
            return currentRequest;
        }

        public bool TryCommit(ViewportRenderRequest request, ViewportPixelBlock block)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (block == null) throw new ArgumentNullException("block");
            if (!ReferenceEquals(request, currentRequest) || request.CancellationToken.IsCancellationRequested) return false;
            CacheBlock(request.ImageKey, block.SourceRectangle, block.Pixels);
            return true;
        }

        public void CacheBlock(string imageKey, Rectangle sourceRectangle, byte[] pixels)
        {
            if (String.IsNullOrEmpty(imageKey)) throw new ArgumentException("图片标识不能为空。", "imageKey");
            if (pixels == null) throw new ArgumentNullException("pixels");
            if (pixels.LongLength > maximumCachedBytes) return;

            var existing = Find(imageKey, sourceRectangle);
            if (existing != null)
            {
                CachedByteCount -= existing.Value.Pixels.LongLength;
                cache.Remove(existing);
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

        private LinkedListNode<CacheEntry> Find(string imageKey, Rectangle sourceRectangle)
        {
            for (var node = cache.First; node != null; node = node.Next)
            {
                if (String.Equals(node.Value.ImageKey, imageKey, StringComparison.Ordinal)
                    && node.Value.SourceRectangle == sourceRectangle) return node;
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
            public byte[] Pixels { get; private set; }
        }
    }
}
