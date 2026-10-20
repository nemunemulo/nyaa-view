using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NyaaView
{
    public class ZipEntryInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
    }

    public class ZipThumbnailResult
    {
        public string ZipPath { get; set; }
        public string ZipName { get; set; }
        public long FileSize { get; set; }
        public int TotalImages { get; set; }
        public List<Bitmap> Thumbnails { get; set; }

        public ZipThumbnailResult()
        {
            Thumbnails = new List<Bitmap>();
        }
    }

    public class ZipEngine : IDisposable
    {
        private static readonly HashSet<string> ImageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
            ".tif", ".tiff", ".jxr", ".wdp", ".hdp", ".ico",
            ".tga", ".qoi", ".psd", ".dds",
            ".ppm", ".pgm", ".pnm"
        };

        private string _zipPath;
        private ZipArchive _archive;
        private List<ZipArchiveEntry> _imageEntries = new List<ZipArchiveEntry>();
        private readonly Dictionary<int, Bitmap> _cache = new Dictionary<int, Bitmap>();
        private readonly Dictionary<int, long> _cacheSizes = new Dictionary<int, long>();
        private readonly HashSet<int> _protectedIndices = new HashSet<int>();
        private long _totalCacheBytes = 0;
        private int _currentIndex = 0;
        private CacheLimitMode _cacheMode = CacheLimitMode.MegaBytes;
        private int _cacheLimitMb = 256;
        private int _cacheWindow = 10;
        private readonly object _lock = new object();

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

        public void SetCurrentIndex(int index)
        {
            lock (_lock)
            {
                _currentIndex = index;
            }
        }

        public CacheLimitMode CacheMode
        {
            get { return _cacheMode; }
            set
            {
                lock (_lock)
                {
                    _cacheMode = value;
                    CleanCache();
                }
            }
        }

        public int CacheLimitMb
        {
            get { return _cacheLimitMb; }
            set
            {
                lock (_lock)
                {
                    _cacheLimitMb = Math.Max(32, Math.Min(65536, value));
                    CleanCache();
                }
            }
        }

        public int CacheWindow
        {
            get { return _cacheWindow; }
            set
            {
                lock (_lock)
                {
                    _cacheWindow = Math.Max(1, Math.Min(50, value));
                    CleanCache();
                }
            }
        }

        public long TotalCacheBytes { get { return _totalCacheBytes; } }
        public double TotalCacheMb { get { return _totalCacheBytes / (1024.0 * 1024.0); } }
        public int CachedImageCount { get { return _cache.Count; } }

        public int Count { get { return _imageEntries.Count; } }
        public string ZipPath { get { return _zipPath; } }

        public List<ZipEntryInfo> LoadZip(string zipPath)
        {
            Close();
            _zipPath = zipPath;

            FileStream fs = null;
            try
            {
                fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                
                // 한글 파일명 및 UTF-8 호환 인코딩
                Encoding enc = Encoding.Default;
                try { enc = Encoding.GetEncoding(949); } catch { }
                _archive = new ZipArchive(fs, ZipArchiveMode.Read, false, enc);

                _imageEntries = _archive.Entries
                    .Where(e => !e.FullName.EndsWith("/") && 
                                !e.FullName.Contains("__MACOSX") &&
                                ImageExts.Contains(Path.GetExtension(e.Name)))
                    .OrderBy(e => e.FullName, new NaturalStringComparer())
                    .ToList();
            }
            catch
            {
                if (_archive != null)
                {
                    _archive.Dispose();
                    _archive = null;
                }
                else if (fs != null)
                {
                    fs.Dispose();
                }
                throw;
            }

            var list = new List<ZipEntryInfo>();
            for (int i = 0; i < _imageEntries.Count; i++)
            {
                list.Add(new ZipEntryInfo
                {
                    Index = i,
                    Name = _imageEntries[i].FullName.Replace('\\', '/'),
                    Size = _imageEntries[i].Length
                });
            }
            return list;
        }

        public string GetEntryName(int index)
        {
            if (index >= 0 && index < _imageEntries.Count)
                return _imageEntries[index].FullName.Replace('\\', '/');
            return string.Empty;
        }

        public long GetEntrySize(int index)
        {
            if (index >= 0 && index < _imageEntries.Count)
                return _imageEntries[index].Length;
            return 0;
        }

        public DateTime? GetEntryDateTime(int index)
        {
            if (index >= 0 && index < _imageEntries.Count)
                return _imageEntries[index].LastWriteTime.DateTime;
            return null;
        }

        public Bitmap GetImage(int index, bool updateCurrentIndex = true)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _imageEntries.Count) return null;
                if (updateCurrentIndex)
                {
                    _currentIndex = index;
                }

                if (_cache.ContainsKey(index))
                {
                    CleanCache();
                    return _cache[index];
                }

                var entry = _imageEntries[index];
                Bitmap bmp = LoadBitmapFromEntry(entry);
                if (bmp != null)
                {
                    long bytes = EstimateBitmapBytes(bmp);
                    _cache[index] = bmp;
                    _cacheSizes[index] = bytes;
                    _totalCacheBytes += bytes;
                    CleanCache();
                }

                return bmp;
            }
        }

        public Size? GetImageSize(int index)
        {
            lock (_lock)
            {
                Bitmap bmp;
                if (_cache.TryGetValue(index, out bmp) && bmp != null)
                {
                    return new Size(bmp.Width, bmp.Height);
                }
                return null;
            }
        }

        private Bitmap LoadBitmapFromEntry(ZipArchiveEntry entry)
        {
            if (entry == null) return null;
            if (entry.Length > ImageLoader.MaxStreamBytes)
            {
                System.Diagnostics.Debug.WriteLine("ZipEngine: 압축 해제 용량(256MB) 초과로 엔트리를 건너뜁니다: " + entry.FullName);
                return null;
            }

            try
            {
                using (Stream s = entry.Open())
                {
                    return ImageLoader.LoadFromStream(s, entry.Name);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ZipEngine: 엔트리 스트림 열기/디코딩 실패 (" + entry.FullName + "): " + ex.Message);
                return null;
            }
        }

        public static long EstimateBitmapBytes(Bitmap bmp)
        {
            if (bmp == null) return 0;
            int bpp = 4;
            switch (bmp.PixelFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb: bpp = 3; break;
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed: bpp = 1; break;
                case System.Drawing.Imaging.PixelFormat.Format16bppRgb565:
                case System.Drawing.Imaging.PixelFormat.Format16bppArgb1555: bpp = 2; break;
                case System.Drawing.Imaging.PixelFormat.Format48bppRgb: bpp = 6; break;
                case System.Drawing.Imaging.PixelFormat.Format64bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format64bppPArgb: bpp = 8; break;
                default: bpp = 4; break;
            }
            return (long)bmp.Width * bmp.Height * bpp;
        }

        private void EvictKey(int key)
        {
            Bitmap bmp;
            if (_cache.TryGetValue(key, out bmp))
            {
                if (bmp != null)
                {
                    IDisposable stream = bmp.Tag as IDisposable;
                    if (stream != null)
                    {
                        try { stream.Dispose(); } catch { }
                    }
                    bmp.Dispose();
                }
                _cache.Remove(key);
            }

            long size;
            if (_cacheSizes.TryGetValue(key, out size))
            {
                _totalCacheBytes = Math.Max(0, _totalCacheBytes - size);
                _cacheSizes.Remove(key);
            }
        }

        private void CleanCache()
        {
            if (_cache.Count == 0) return;

            if (_cacheMode == CacheLimitMode.MegaBytes)
            {
                long maxBytes = (long)_cacheLimitMb * 1024 * 1024;
                while (_totalCacheBytes > maxBytes && _cache.Count > 1)
                {
                    int worstKey = -1;
                    int maxDist = -1;

                    foreach (int k in _cache.Keys)
                    {
                        // 현재 열람 중인 페이지 및 화면에 표시 중인 보호 페이지는 절대 제거하지 않음!
                        if (k == _currentIndex || _protectedIndices.Contains(k)) continue;

                        int dist = Math.Abs(k - _currentIndex);
                        if (dist > maxDist)
                        {
                            maxDist = dist;
                            worstKey = k;
                        }
                    }

                    if (worstKey == -1) break; // 지울 수 있는(화면 밖) 캐시가 없으면 보호를 위해 탈출
                    EvictKey(worstKey);
                }
            }
            else
            {
                int min = _currentIndex - _cacheWindow;
                int max = _currentIndex + _cacheWindow;

                var keysToRemove = new List<int>();
                foreach (int k in _cache.Keys)
                {
                    if (k == _currentIndex || _protectedIndices.Contains(k)) continue;
                    if (k < min || k > max)
                    {
                        keysToRemove.Add(k);
                    }
                }

                foreach (int key in keysToRemove)
                {
                    EvictKey(key);
                }
            }
        }

        // 탐색기용 썸네일 스트립 고속 추출 (1~N장)
        public static ZipThumbnailResult ExtractThumbnails(string zipPath, int count = 5)
        {
            var res = new ZipThumbnailResult
            {
                ZipPath = zipPath,
                ZipName = Path.GetFileName(zipPath),
                FileSize = new FileInfo(zipPath).Length
            };

            try
            {
                using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Encoding enc = Encoding.Default;
                    try { enc = Encoding.GetEncoding(949); } catch { }
                    using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read, false, enc))
                    {
                        var entries = archive.Entries
                            .Where(e => !e.FullName.EndsWith("/") && 
                                        !e.FullName.Contains("__MACOSX") &&
                                        ImageExts.Contains(Path.GetExtension(e.Name)))
                            .OrderBy(e => e.FullName, new NaturalStringComparer())
                            .ToList();

                        res.TotalImages = entries.Count;
                        int targetCount = Math.Min(count, entries.Count);

                        for (int i = 0; i < targetCount; i++)
                        {
                            var entry = entries[i];
                            using (Stream s = entry.Open())
                            using (Bitmap orig = ImageLoader.LoadFromStream(s, entry.Name))
                            {
                                if (orig == null || orig.Width <= 0 || orig.Height <= 0) continue;

                                int tw = 120;
                                int th = (int)((float)orig.Height / orig.Width * tw);
                                if (th <= 0) th = 90;
                                if (th > 140) th = 140;

                                Bitmap thumb = new Bitmap(tw, th);
                                using (Graphics g = Graphics.FromImage(thumb))
                                {
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
                                    g.DrawImage(orig, 0, 0, tw, th);
                                }
                                res.Thumbnails.Add(thumb);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("썸네일 추출 에러: " + ex.Message);
            }

            return res;
        }

        public void Close()
        {
            lock (_lock)
            {
                foreach (var bmp in _cache.Values)
                {
                    if (bmp != null)
                    {
                        IDisposable stream = bmp.Tag as IDisposable;
                        if (stream != null)
                        {
                            try { stream.Dispose(); } catch { }
                        }
                        bmp.Dispose();
                    }
                }
                _cache.Clear();
                _cacheSizes.Clear();
                _totalCacheBytes = 0;
                _imageEntries.Clear();

                if (_archive != null)
                {
                    _archive.Dispose();
                    _archive = null;
                }
            }
        }

        public void Dispose()
        {
            Close();
        }
    }

    // Windows 탐색기 방식의 자연 정렬 (1~9화 -> 10~99화 -> 100~999화 -> 1000~9999화)
    // 단일 파일명은 물론 다층 디렉터리 경로(1화/01.jpg vs 10화/01.jpg)까지 계층별 완벽 자연 정렬
    public class NaturalStringComparer : IComparer<string>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public int Compare(string x, string y)
        {
            if (object.ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // 경로 구분자(/ 및 \)가 포함된 경우 디렉터리 세그먼트별로 분할하여 자연 비교
            if (x.IndexOfAny(new char[] { '/', '\\' }) >= 0 || y.IndexOfAny(new char[] { '/', '\\' }) >= 0)
            {
                string[] partsX = x.Replace('\\', '/').Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string[] partsY = y.Replace('\\', '/').Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                int minLen = Math.Min(partsX.Length, partsY.Length);
                for (int i = 0; i < minLen; i++)
                {
                    bool isLastX = (i == partsX.Length - 1);
                    bool isLastY = (i == partsY.Length - 1);

                    // 같은 계층에서 파일과 디렉터리가 비교되는 경우(예: cover.jpg vs 1화/01.jpg): 루트 파일 선행
                    if (isLastX != isLastY)
                    {
                        return isLastX ? -1 : 1;
                    }

                    int cmp = StrCmpLogicalW(partsX[i], partsY[i]);
                    if (cmp != 0) return cmp;
                }

                return partsX.Length.CompareTo(partsY.Length);
            }

            // 일반 파일명/문자열 단일 자연 비교
            return StrCmpLogicalW(x, y);
        }
    }
}
