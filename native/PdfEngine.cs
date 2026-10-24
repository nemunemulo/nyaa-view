using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NyaaView
{
    public class PdfEngine : IDisposable
    {
        private string _pdfPath;
        private PdfDocument _pdfDoc;
        private int _pageCount = 0;
        private readonly Dictionary<int, Bitmap> _cache = new Dictionary<int, Bitmap>();
        private readonly Dictionary<int, long> _cacheSizes = new Dictionary<int, long>();
        private readonly HashSet<int> _protectedIndices = new HashSet<int>();
        private long _totalCacheBytes = 0;
        private int _cacheLimitMb = 256;
        private int _currentIndex = 0;
        private readonly object _lock = new object();

        public void SetProtectedIndices(IEnumerable<int> indices)
        {
            lock (_lock)
            {
                _protectedIndices.Clear();
                if (indices != null)
                {
                    foreach (int idx in indices) _protectedIndices.Add(idx);
                }
            }
        }

        public int PageCount { get { return _pageCount; } }
        public string PdfPath { get { return _pdfPath; } }
        public int CacheLimitMb
        {
            get { return _cacheLimitMb; }
            set { _cacheLimitMb = value; CleanCache(); }
        }

        public bool LoadPdf(string filePath)
        {
            Close();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            try
            {
                _pdfPath = filePath;
                StorageFile file = WaitWinRt(StorageFile.GetFileFromPathAsync(filePath));
                if (file == null) return false;

                _pdfDoc = WaitWinRt(PdfDocument.LoadFromFileAsync(file));
                if (_pdfDoc != null)
                {
                    _pageCount = (int)_pdfDoc.PageCount;
                    return _pageCount > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PDF 로드 오류: " + ex.Message);
            }

            Close();
            return false;
        }

        public bool IsCached(int pageIndex)
        {
            lock (_lock)
            {
                return _cache.ContainsKey(pageIndex);
            }
        }

        public Bitmap GetImage(int pageIndex, bool updateCurrentIndex = true)
        {
            if (_pdfDoc == null || pageIndex < 0 || pageIndex >= _pageCount) return null;
            if (updateCurrentIndex)
            {
                _currentIndex = pageIndex;
            }

            lock (_lock)
            {
                if (_cache.ContainsKey(pageIndex))
                {
                    return _cache[pageIndex];
                }
            }

            Bitmap bmp = RenderPageDirect(pageIndex);
            if (bmp != null)
            {
                lock (_lock)
                {
                    if (!_cache.ContainsKey(pageIndex))
                    {
                        long size = (long)bmp.Width * bmp.Height * 4;
                        _cache[pageIndex] = bmp;
                        _cacheSizes[pageIndex] = size;
                        _totalCacheBytes += size;
                        CleanCache(pageIndex);
                    }
                }
            }

            return bmp;
        }

        private Bitmap RenderPageDirect(int pageIndex)
        {
            if (_pdfDoc == null || pageIndex < 0 || pageIndex >= _pageCount) return null;

            try
            {
                using (PdfPage page = _pdfDoc.GetPage((uint)pageIndex))
                using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                {
                    PdfPageRenderOptions options = new PdfPageRenderOptions();
                    WaitWinRt(page.RenderToStreamAsync(stream, options));

                    ulong size = stream.Size;
                    if (size == 0) return null;

                    byte[] bytes = new byte[size];
                    using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                    {
                        WaitWinRt(reader.LoadAsync((uint)size));
                        reader.ReadBytes(bytes);
                    }

                    using (MemoryStream ms = new MemoryStream(bytes))
                    using (Image temp = Image.FromStream(ms))
                    {
                        return new Bitmap(temp);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PDF 페이지 렌더링 실패: " + ex.Message);
                return null;
            }
        }

        public string GetPageDimensionsStr(int pageIndex)
        {
            return "";
        }

        private void CleanCache(int keepIndex = -1)
        {
            long limitBytes = (long)_cacheLimitMb * 1024 * 1024;
            if (_totalCacheBytes <= limitBytes) return;

            var keysToEvict = new List<int>(_cache.Keys);
            keysToEvict.Sort((a, b) => Math.Abs(b - _currentIndex).CompareTo(Math.Abs(a - _currentIndex)));

            foreach (int key in keysToEvict)
            {
                if (_totalCacheBytes <= limitBytes) break;
                if (key == _currentIndex || key == keepIndex || _protectedIndices.Contains(key)) continue;

                Bitmap bmp = _cache[key];
                long size = _cacheSizes[key];
                _cache.Remove(key);
                _cacheSizes.Remove(key);
                _totalCacheBytes -= size;
                bmp.Dispose();
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                foreach (var bmp in _cache.Values)
                {
                    try { bmp.Dispose(); } catch { }
                }
                _cache.Clear();
                _cacheSizes.Clear();
                _totalCacheBytes = 0;
            }

            if (_pdfDoc != null)
            {
                try { _pdfDoc = null; } catch { }
            }
            _pdfPath = null;
            _pageCount = 0;
            _currentIndex = 0;
        }

        public void Dispose()
        {
            Close();
        }

        // WinRT 비동기 작업을 안전하게 동기 대기 (30초 타임아웃 가드 적용)
        private static T WaitWinRt<T>(IAsyncOperation<T> op)
        {
            if (op == null) return default(T);
            int elapsedMs = 0;
            while (op.Status == AsyncStatus.Started)
            {
                Thread.Sleep(1);
                elapsedMs++;
                if (elapsedMs > 30000)
                {
                    try { op.Cancel(); } catch { }
                    return default(T);
                }
            }
            if (op.Status == AsyncStatus.Error)
            {
                throw op.ErrorCode;
            }
            return op.GetResults();
        }

        private static void WaitWinRt(IAsyncAction action)
        {
            if (action == null) return;
            int elapsedMs = 0;
            while (action.Status == AsyncStatus.Started)
            {
                Thread.Sleep(1);
                elapsedMs++;
                if (elapsedMs > 30000)
                {
                    try { action.Cancel(); } catch { }
                    return;
                }
            }
            action.GetResults();
        }

        public static PdfThumbnailResult ExtractThumbnails(string pdfPath, int count)
        {
            var res = new PdfThumbnailResult { PdfPath = pdfPath };
            try
            {
                using (var engine = new PdfEngine())
                {
                    if (engine.LoadPdf(pdfPath))
                    {
                        res.TotalPages = engine.PageCount;
                        int targetCount = Math.Min(count, engine.PageCount);
                        for (int i = 0; i < targetCount; i++)
                        {
                            using (Bitmap orig = engine.RenderPageDirect(i))
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
                System.Diagnostics.Debug.WriteLine("PDF 썸네일 추출 에러: " + ex.Message);
            }
            return res;
        }
    }

    public class PdfThumbnailResult
    {
        public string PdfPath { get; set; }
        public int TotalPages { get; set; }
        public List<Bitmap> Thumbnails { get; set; }

        public PdfThumbnailResult()
        {
            Thumbnails = new List<Bitmap>();
        }
    }
}
