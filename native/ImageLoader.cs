using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace NyaaView
{
    public static class ImageLoader
    {
        // 파일/스트림 최대 허용 크기 (256MB - 악의적 Zip Bomb 및 비정상 스트림 방어)
        public const long MaxStreamBytes = 256L * 1024 * 1024;

        // 디코딩 후 픽셀 폭탄 (Decompression Bomb / Pixel Flood) 방어 기준치
        public const int MaxDimension = 200000; // 가로 또는 세로 최대 허용 픽셀 (초광각 파노라마 지원)
        public const long MaxPixelCount = 500000000L; // 최대 5억 픽셀 (스마트폰 200MP 최고화소 및 초광각 파노라마 모드 완벽 지원)

        // 사용자 설정: 초고화소 제한 완전 해제 (무제한 로드 모드)
        public static bool UnlimitedPixelLimit { get; set; }

        public static bool ValidateDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            if (UnlimitedPixelLimit) return true; // 설정에서 제한 해제 시 무제한 허용
            if (width > MaxDimension || height > MaxDimension) return false;
            long pixels = (long)width * (long)height;
            if (pixels > MaxPixelCount) return false;
            return true;
        }

        // 파일 또는 스트림에서 다양한 이미지 포맷 통합 디코딩:
        // JPEG, PNG, GIF, BMP, WebP, TIFF, JXR, ICO, TGA, QOI, PSD, DDS, PPM, PGM, PNM 및 WIC 지원 포맷
        public static Bitmap LoadFromStream(Stream stream, string fileName = "")
        {
            if (stream == null) return null;

            byte[] bytes;
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[81920];
                    int read;
                    long total = 0;
                    while ((read = stream.Read(buf, 0, buf.Length)) > 0)
                    {
                        total += read;
                        if (!UnlimitedPixelLimit && total > MaxStreamBytes)
                        {
                            System.Diagnostics.Debug.WriteLine("ImageLoader: 입력 스트림이 최대 허용 용량(256MB)을 초과하여 중단되었습니다.");
                            return null;
                        }
                        ms.Write(buf, 0, read);
                    }
                    bytes = ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ImageLoader: 스트림 읽기 중 오류 발생 (손상된 데이터): " + ex.Message);
                return null;
            }

            if (bytes == null || bytes.Length == 0) return null;

            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            // 1. QOI (Quite OK Image format)
            if (ext == ".qoi" || (bytes.Length >= 4 && bytes[0] == 'q' && bytes[1] == 'o' && bytes[2] == 'i' && bytes[3] == 'f'))
            {
                Bitmap qoiBmp = QoiDecoder.Decode(bytes);
                if (qoiBmp != null) return qoiBmp;
            }

            // 2. PSD (Adobe Photoshop)
            if (ext == ".psd" || (bytes.Length >= 4 && bytes[0] == '8' && bytes[1] == 'B' && bytes[2] == 'P' && bytes[3] == 'S'))
            {
                Bitmap psdBmp = PsdDecoder.Decode(bytes);
                if (psdBmp != null) return psdBmp;
            }

            // 3. DDS (DirectDraw Surface)
            if (ext == ".dds" || (bytes.Length >= 4 && bytes[0] == 'D' && bytes[1] == 'D' && bytes[2] == 'S' && bytes[3] == ' '))
            {
                Bitmap ddsBmp = DdsDecoder.Decode(bytes);
                if (ddsBmp != null) return ddsBmp;
            }

            // 4. TGA (Truevision Targa)
            if (ext == ".tga")
            {
                Bitmap tgaBmp = TgaDecoder.Decode(bytes);
                if (tgaBmp != null) return tgaBmp;
            }

            // 5. Netpbm (PPM / PGM / PNM)
            if (ext == ".ppm" || ext == ".pgm" || ext == ".pnm" || (bytes.Length >= 2 && bytes[0] == 'P' && bytes[1] >= '1' && bytes[1] <= '6'))
            {
                Bitmap pnmBmp = NetpbmDecoder.Decode(bytes);
                if (pnmBmp != null) return pnmBmp;
            }

            // 6. ICO (Windows Icon)
            if (ext == ".ico")
            {
                try
                {
                    using (MemoryStream icoMs = new MemoryStream(bytes))
                    using (Icon ico = new Icon(icoMs, 256, 256))
                    {
                        return ico.ToBitmap();
                    }
                }
                catch
                {
                    // WIC로 폴백
                }
            }

            // 7. WebP 및 WIC 전용 코덱 포맷
            if (ext == ".webp" || ext == ".jxr" || ext == ".wdp" || ext == ".hdp" || ext == ".heic" || ext == ".heif" || ext == ".avif")
            {
                try
                {
                    Bitmap wicBmp = LoadViaWic(bytes);
                    if (wicBmp != null) return wicBmp;
                }
                catch { }
            }

            // 8. GIF 포맷은 멀티프레임 애니메이션 및 프레임 탐색을 위해 스트림을 보존하여 직접 디코딩
            bool isGif = (ext == ".gif") || (bytes.Length >= 6 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'8');
            if (isGif)
            {
                try
                {
                    MemoryStream gifMs = new MemoryStream(bytes);
                    Bitmap gifBmp = (Bitmap)Image.FromStream(gifMs);
                    if (!ValidateDimensions(gifBmp.Width, gifBmp.Height))
                    {
                        gifBmp.Dispose();
                        gifMs.Dispose();
                        return null;
                    }
                    gifBmp.Tag = gifMs; // 멀티프레임 디코딩을 위해 스트림 생명주기 유지
                    return gifBmp;
                }
                catch
                {
                    // 실패 시 일반 디코딩으로 폴백
                }
            }

            // 9. 고화질 사진 포맷(JPEG, PNG, TIFF)은 WIC SIMD 가속 디코더 우선 적용 (GDI+ 대비 2~3배 고속 & EXIF 자동 회전)
            try
            {
                Bitmap wicBmp = LoadViaWic(bytes);
                if (wicBmp != null) return wicBmp;
            }
            catch { }

            // 10. WIC 미지원 또는 실패 시 GDI+ 디코딩으로 안전한 폴백
            try
            {
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image temp = Image.FromStream(ms))
                {
                    if (!ValidateDimensions(temp.Width, temp.Height))
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("ImageLoader: 이미지 해상도({0}x{1})가 허용 한도를 초과하여 차단되었습니다.", temp.Width, temp.Height));
                        return null;
                    }

                    Bitmap result = new Bitmap(temp);
                    try
                    {
                        foreach (var prop in temp.PropertyItems)
                        {
                            try { result.SetPropertyItem(prop); } catch { }
                        }
                    }
                    catch { }

                    ushort gdiOri = GetGdiOrientation(temp);
                    if (gdiOri > 1)
                    {
                        ApplyOrientation(result, gdiOri);
                    }

                    return result;
                }
            }
            catch
            {
                // 매직 넘버 기반 커스텀 디코더 재시도
                Bitmap fallback = QoiDecoder.Decode(bytes);
                if (fallback != null) return fallback;
                fallback = PsdDecoder.Decode(bytes);
                if (fallback != null) return fallback;
                fallback = DdsDecoder.Decode(bytes);
                if (fallback != null) return fallback;
                fallback = TgaDecoder.Decode(bytes);
                if (fallback != null) return fallback;
                fallback = NetpbmDecoder.Decode(bytes);
                if (fallback != null) return fallback;
            }

            return null;
        }

        public static Bitmap LoadFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            try
            {
                FileInfo fi = new FileInfo(filePath);
                if (!UnlimitedPixelLimit && fi.Length > MaxStreamBytes)
                {
                    System.Diagnostics.Debug.WriteLine("ImageLoader: 파일 크기(256MB) 초과로 로드를 건너뜁니다: " + filePath);
                    return null;
                }
            }
            catch { }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".gif")
            {
                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        Bitmap wicBmp = LoadViaWic(fs);
                        if (wicBmp != null) return wicBmp;
                    }
                }
                catch { }
            }

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return LoadFromStream(fs, filePath);
            }
        }

        public static ushort GetWicOrientation(BitmapFrame frame)
        {
            if (frame == null) return 1;
            try
            {
                var meta = frame.Metadata as BitmapMetadata;
                if (meta != null)
                {
                    object val = null;
                    try { val = meta.GetQuery("/app1/ifd/{ushort=274}"); } catch { }
                    if (val == null) { try { val = meta.GetQuery("System.Photo.Orientation"); } catch { } }
                    if (val == null) { try { val = meta.GetQuery("/ifd/{ushort=274}"); } catch { } }
                    if (val != null)
                    {
                        return Convert.ToUInt16(val);
                    }
                }
            }
            catch { }
            return 1;
        }

        public static void ApplyOrientation(Bitmap bmp, ushort orientation)
        {
            if (bmp == null || orientation <= 1) return;
            RotateFlipType flip = RotateFlipType.RotateNoneFlipNone;
            switch (orientation)
            {
                case 2: flip = RotateFlipType.RotateNoneFlipX; break;
                case 3: flip = RotateFlipType.Rotate180FlipNone; break;
                case 4: flip = RotateFlipType.Rotate180FlipX; break;
                case 5: flip = RotateFlipType.Rotate90FlipX; break;
                case 6: flip = RotateFlipType.Rotate90FlipNone; break;
                case 7: flip = RotateFlipType.Rotate270FlipX; break;
                case 8: flip = RotateFlipType.Rotate270FlipNone; break;
            }
            if (flip != RotateFlipType.RotateNoneFlipNone)
            {
                bmp.RotateFlip(flip);
                try { bmp.RemovePropertyItem(0x0112); } catch { }
            }
        }

        public static ushort GetGdiOrientation(Image img)
        {
            if (img == null) return 1;
            try
            {
                if (Array.IndexOf(img.PropertyIdList, 0x0112) >= 0)
                {
                    var prop = img.GetPropertyItem(0x0112);
                    if (prop != null && prop.Value != null && prop.Value.Length >= 2)
                    {
                        return BitConverter.ToUInt16(prop.Value, 0);
                    }
                }
            }
            catch { }
            return 1;
        }

        public static Bitmap LoadViaWic(Stream stream)
        {
            if (stream == null) return null;
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];

            if (!ValidateDimensions(frame.PixelWidth, frame.PixelHeight))
            {
                System.Diagnostics.Debug.WriteLine(string.Format("ImageLoader(WIC): 이미지 해상도({0}x{1})가 허용 한도를 초과하여 차단되었습니다.", frame.PixelWidth, frame.PixelHeight));
                return null;
            }

            int width = frame.PixelWidth;
            int height = frame.PixelHeight;

            BitmapSource converted = frame;
            if (frame.Format != System.Windows.Media.PixelFormats.Bgra32)
            {
                converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            }

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = null;
            try
            {
                data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                int stride = data.Stride;
                converted.CopyPixels(System.Windows.Int32Rect.Empty, data.Scan0, stride * height, stride);
            }
            finally
            {
                if (data != null) bmp.UnlockBits(data);
            }

            ushort orientation = GetWicOrientation(frame);
            if (orientation > 1)
            {
                ApplyOrientation(bmp, orientation);
            }

            return bmp;
        }

        public static Bitmap LoadViaWic(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                return LoadViaWic(ms);
            }
        }
    }
}
