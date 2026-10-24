using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace NyaaView
{
    public class SingleImageCache : IDisposable
    {
        private readonly Dictionary<int, Bitmap> _cache = new Dictionary<int, Bitmap>();
        private readonly Dictionary<int, long> _cacheSizes = new Dictionary<int, long>();
        private readonly HashSet<int> _protectedIndices = new HashSet<int>();
        private long _totalCacheBytes = 0;
        private readonly object _lock = new object();
        private int _limitMb = 512;

        public int LimitMb
        {
            get { return _limitMb; }
            set
            {
                lock (_lock)
                {
                    _limitMb = Math.Max(64, value);
                    CleanCache(-1);
                }
            }
        }

        public long TotalCacheBytes { get { return _totalCacheBytes; } }
        public int CachedCount { get { lock (_lock) { return _cache.Count; } } }

        public void SetProtectedIndices(IEnumerable<int> indices)
        {
            lock (_lock)
            {
                _protectedIndices.Clear();
                if (indices != null)
                {
                    foreach (int idx in indices)
                    {
                        _protectedIndices.Add(idx);
                    }
                }
            }
        }

        public bool IsCached(int index)
        {
            lock (_lock)
            {
                return _cache.ContainsKey(index);
            }
        }

        public bool TryGet(int index, out Bitmap bmp)
        {
            lock (_lock)
            {
                return _cache.TryGetValue(index, out bmp);
            }
        }

        public void Put(int index, Bitmap bmp)
        {
            if (bmp == null) return;
            lock (_lock)
            {
                if (_cache.ContainsKey(index))
                {
                    return;
                }

                long size = ZipEngine.EstimateBitmapBytes(bmp);
                _cache[index] = bmp;
                _cacheSizes[index] = size;
                _totalCacheBytes += size;
                CleanCache(index);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                foreach (var kvp in _cache)
                {
                    if (kvp.Value != null)
                    {
                        IDisposable stream = kvp.Value.Tag as IDisposable;
                        if (stream != null) { try { stream.Dispose(); } catch { } }
                        try { kvp.Value.Dispose(); } catch { }
                    }
                }
                _cache.Clear();
                _cacheSizes.Clear();
                _protectedIndices.Clear();
                _totalCacheBytes = 0;
            }
        }

        public void Dispose()
        {
            Clear();
        }

        private void CleanCache(int currentIndex)
        {
            long limitBytes = (long)_limitMb * 1024 * 1024;
            if (_totalCacheBytes <= limitBytes) return;

            // 현재 표시 중이거나 보호된 인덱스를 제외하고, 현재 위치에서 가장 먼 인덱스부터 축출
            var evictList = _cache.Keys
                .Where(k => !_protectedIndices.Contains(k))
                .OrderByDescending(k => Math.Abs(k - currentIndex))
                .ToList();

            foreach (int key in evictList)
            {
                if (_totalCacheBytes <= limitBytes) break;

                Bitmap bmp;
                if (_cache.TryGetValue(key, out bmp))
                {
                    if (bmp != null)
                    {
                        IDisposable stream = bmp.Tag as IDisposable;
                        if (stream != null) { try { stream.Dispose(); } catch { } }
                        try { bmp.Dispose(); } catch { }
                    }
                    _cache.Remove(key);
                }

                long sz;
                if (_cacheSizes.TryGetValue(key, out sz))
                {
                    _totalCacheBytes = Math.Max(0, _totalCacheBytes - sz);
                    _cacheSizes.Remove(key);
                }
            }
        }
    }
}
