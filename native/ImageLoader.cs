using System;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace NyaaView
{
    public static class ImageLoader
    {
        // 파일 또는 스트림에서 다양한 이미지 포맷 통합 디코딩:
        // JPEG, PNG, GIF, BMP, WebP, TIFF, JXR, ICO, TGA, QOI, PSD, DDS, PPM, PGM, PNM 및 WIC 지원 포맷
        public static Bitmap LoadFromStream(Stream stream, string fileName = "")
        {
            byte[] bytes;
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                bytes = ms.ToArray();
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

            // 7. WebP 및 JXR/WDP/HDP는 Windows WIC로 고속 디코딩
            if (ext == ".webp" || ext == ".jxr" || ext == ".wdp" || ext == ".hdp")
            {
                try
                {
                    return LoadViaWic(bytes);
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
                    gifBmp.Tag = gifMs; // 멀티프레임 디코딩을 위해 스트림 생명주기 유지
                    return gifBmp;
                }
                catch
                {
                    // 실패 시 일반 디코딩으로 폴백
                }
            }

            // 9. 표준 포맷 (JPEG, PNG, TIFF, BMP) GDI+ 디코딩 시도
            try
            {
                using (MemoryStream ms = new MemoryStream(bytes))
                using (Image temp = Image.FromStream(ms))
                {
                    Bitmap result = new Bitmap(temp);
                    try
                    {
                        foreach (var prop in temp.PropertyItems)
                        {
                            try { result.SetPropertyItem(prop); } catch { }
                        }
                    }
                    catch { }
                    return result;
                }
            }
            catch
            {
                // GDI+ 실패 시 WIC로 강력한 폴백 (WIC는 TIFF, JXR, HEIC, AVIF, RAW 등 시스템 코덱 전부 지원)
                try
                {
                    return LoadViaWic(bytes);
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
            }

            return null;
        }

        public static Bitmap LoadFromFile(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return LoadFromStream(fs, filePath);
            }
        }

        private static Bitmap LoadViaWic(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];

                using (MemoryStream outMs = new MemoryStream())
                {
                    var enc = new BmpBitmapEncoder();
                    enc.Frames.Add(frame);
                    enc.Save(outMs);
                    outMs.Position = 0;
                    using (Image temp = Image.FromStream(outMs))
                    {
                        return new Bitmap(temp);
                    }
                }
            }
        }
    }
}
