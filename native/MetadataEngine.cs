using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace NyaaView
{
    public class MetadataSection
    {
        public string Title { get; set; }
        public List<KeyValuePair<string, string>> Items { get; set; }

        public MetadataSection(string title)
        {
            Title = title;
            Items = new List<KeyValuePair<string, string>>();
        }

        public void Add(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Items.Add(new KeyValuePair<string, string>(key, value));
            }
        }
    }

    public class ImageInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string ArchivePath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime? FileDate { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string FormatName { get; set; }
        public string PixelFormatStr { get; set; }
        
        public bool IsGif { get; set; }
        public int GifFrameCount { get; set; }
        public int GifCurrentFrame { get; set; }
        public int GifDelayMs { get; set; }

        public List<MetadataSection> Sections { get; set; }

        public ImageInfo()
        {
            Sections = new List<MetadataSection>();
        }
    }

    public static class MetadataEngine
    {
        public static ImageInfo Extract(Bitmap bmp, string fileName, string fullPath, string archivePath, long fileSizeBytes, DateTime? fileDate, bool isGif, int gifFrameCount, int gifCurrentFrame, int gifDelayMs)
        {
            ImageInfo info = new ImageInfo();
            info.FileName = fileName ?? "";
            info.FilePath = fullPath ?? "";
            info.ArchivePath = archivePath ?? "";
            info.FileSizeBytes = fileSizeBytes;
            info.FileDate = fileDate;
            info.IsGif = isGif;
            info.GifFrameCount = gifFrameCount;
            info.GifCurrentFrame = gifCurrentFrame;
            info.GifDelayMs = gifDelayMs;

            if (bmp != null)
            {
                info.Width = bmp.Width;
                info.Height = bmp.Height;
                info.PixelFormatStr = FormatPixelDepth(bmp.PixelFormat);
            }

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            info.FormatName = DetectFormatName(ext);

            // 1. 파일 정보 섹션
            MetadataSection fileSec = new MetadataSection("📁 파일 정보");
            fileSec.Add("파일명", info.FileName);
            if (!string.IsNullOrEmpty(info.ArchivePath))
            {
                fileSec.Add("압축 파일", Path.GetFileName(info.ArchivePath));
                fileSec.Add("압축 경로", info.ArchivePath);
            }
            else if (!string.IsNullOrEmpty(info.FilePath))
            {
                fileSec.Add("폴더 경로", Path.GetDirectoryName(info.FilePath));
            }

            if (info.FileSizeBytes > 0)
            {
                fileSec.Add("파일 크기", FormatFileSize(info.FileSizeBytes));
            }
            if (info.FileDate.HasValue)
            {
                fileSec.Add("수정 일시", info.FileDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            info.Sections.Add(fileSec);

            // 2. 이미지 사양 섹션
            MetadataSection specSec = new MetadataSection("🖼️ 이미지 사양");
            specSec.Add("포맷", info.FormatName);
            if (bmp != null)
            {
                specSec.Add("해상도", string.Format("{0} × {1} px", bmp.Width, bmp.Height));
                double mp = (bmp.Width * (double)bmp.Height) / 1000000.0;
                specSec.Add("메가픽셀", string.Format("{0:0.##} MP", mp));
                specSec.Add("화면 비율", GetAspectRatio(bmp.Width, bmp.Height));
                specSec.Add("색상 심도", info.PixelFormatStr);
            }

            if (isGif && gifFrameCount > 1)
            {
                specSec.Add("GIF 프레임", string.Format("{0} / {1} 프레임", gifCurrentFrame + 1, gifFrameCount));
                specSec.Add("프레임 지연", string.Format("{0} ms ({1:0.#} FPS)", gifDelayMs, gifDelayMs > 0 ? 1000.0 / gifDelayMs : 0));
            }
            info.Sections.Add(specSec);

            // 3. EXIF 촬영 메타데이터 섹션
            MetadataSection exifSec = new MetadataSection("📷 카메라 & EXIF 메타데이터");
            bool hasExif = false;

            if (bmp != null)
            {
                try
                {
                    hasExif = ParseGdiExif(bmp, exifSec);
                }
                catch { }
            }

            // 파일 경로가 존재하고 아직 EXIF를 못 읽었거나 WebP인 경우 WIC로 시도
            if (!hasExif && !string.IsNullOrEmpty(info.FilePath) && File.Exists(info.FilePath))
            {
                try
                {
                    hasExif = ParseWicExif(info.FilePath, exifSec);
                }
                catch { }
            }

            if (!hasExif)
            {
                exifSec.Add("메타데이터", "(카메라 EXIF 메타데이터 없음)");
            }
            info.Sections.Add(exifSec);

            return info;
        }

        private static bool ParseGdiExif(Bitmap bmp, MetadataSection sec)
        {
            if (bmp.PropertyItems == null || bmp.PropertyItems.Length == 0)
                return false;

            Dictionary<int, PropertyItem> dict = new Dictionary<int, PropertyItem>();
            foreach (PropertyItem item in bmp.PropertyItems)
            {
                dict[item.Id] = item;
            }

            int initialCount = sec.Items.Count;

            // 제조사 및 기종
            string make = GetAscii(dict, 0x010F);
            string model = GetAscii(dict, 0x0110);
            if (!string.IsNullOrEmpty(make) || !string.IsNullOrEmpty(model))
            {
                string camera = (make + " " + model).Trim();
                sec.Add("카메라 기종", camera);
            }

            // 렌즈 모델
            string lens = GetAscii(dict, 0xA434);
            if (string.IsNullOrEmpty(lens)) lens = GetAscii(dict, 0xA433);
            if (!string.IsNullOrEmpty(lens))
            {
                sec.Add("렌즈 모델", lens);
            }

            // 촬영 일시
            string dtOrig = GetAscii(dict, 0x9003);
            if (string.IsNullOrEmpty(dtOrig)) dtOrig = GetAscii(dict, 0x0132);
            if (!string.IsNullOrEmpty(dtOrig))
            {
                sec.Add("촬영 일시", dtOrig);
            }

            // 셔터 스피드 (노출 시간)
            if (dict.ContainsKey(0x829A))
            {
                sec.Add("셔터 스피드", FormatExposureTime(dict[0x829A]));
            }

            // 조리개 F-값
            if (dict.ContainsKey(0x829D))
            {
                sec.Add("조리개 값", FormatFNumber(dict[0x829D]));
            }

            // ISO 감도
            if (dict.ContainsKey(0x8827))
            {
                ushort iso = BitConverter.ToUInt16(dict[0x8827].Value, 0);
                sec.Add("ISO 감도", "ISO " + iso);
            }

            // 초점 거리
            if (dict.ContainsKey(0x920A))
            {
                string fl = FormatRational(dict[0x920A], "mm");
                if (dict.ContainsKey(0xA405))
                {
                    ushort fl35 = BitConverter.ToUInt16(dict[0xA405].Value, 0);
                    fl += string.Format(" (35mm 환산: {0}mm)", fl35);
                }
                sec.Add("초점 거리", fl);
            }

            // 노출 보정
            if (dict.ContainsKey(0x9204))
            {
                sec.Add("노출 보정", FormatExposureBias(dict[0x9204]));
            }

            // 노출 프로그램
            if (dict.ContainsKey(0x8822))
            {
                sec.Add("노출 모드", FormatExposureProgram(dict[0x8822]));
            }

            // 측광 모드
            if (dict.ContainsKey(0x9207))
            {
                sec.Add("측광 모드", FormatMeteringMode(dict[0x9207]));
            }

            // 플래시
            if (dict.ContainsKey(0x9209))
            {
                ushort flash = BitConverter.ToUInt16(dict[0x9209].Value, 0);
                sec.Add("플래시", (flash & 1) != 0 ? "플래시 켜짐" : "플래시 꺼짐");
            }

            // 화면 방향
            if (dict.ContainsKey(0x0112))
            {
                sec.Add("화면 방향", FormatOrientation(dict[0x0112]));
            }

            // 소프트웨어
            string sw = GetAscii(dict, 0x0131);
            if (!string.IsNullOrEmpty(sw))
            {
                sec.Add("소프트웨어", sw);
            }

            return sec.Items.Count > initialCount;
        }

        private static bool ParseWicExif(string path, MetadataSection sec)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    var meta = decoder.Frames[0].Metadata as BitmapMetadata;
                    if (meta != null)
                    {
                        int init = sec.Items.Count;
                        if (!string.IsNullOrEmpty(meta.CameraManufacturer) || !string.IsNullOrEmpty(meta.CameraModel))
                        {
                            sec.Add("카메라 기종", (meta.CameraManufacturer + " " + meta.CameraModel).Trim());
                        }
                        if (!string.IsNullOrEmpty(meta.DateTaken))
                        {
                            sec.Add("촬영 일시", meta.DateTaken);
                        }
                        if (!string.IsNullOrEmpty(meta.ApplicationName))
                        {
                            sec.Add("소프트웨어", meta.ApplicationName);
                        }
                        if (!string.IsNullOrEmpty(meta.Title))
                        {
                            sec.Add("제목/설명", meta.Title);
                        }
                        return sec.Items.Count > init;
                    }
                }
            }
            return false;
        }

        private static string GetAscii(Dictionary<int, PropertyItem> dict, int id)
        {
            if (!dict.ContainsKey(id)) return null;
            PropertyItem item = dict[id];
            if (item.Value == null || item.Value.Length == 0) return null;
            string s = Encoding.UTF8.GetString(item.Value).Trim('\0', ' ', '\r', '\n');
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static string FormatExposureTime(PropertyItem item)
        {
            if (item.Value.Length < 8) return "";
            uint num = BitConverter.ToUInt32(item.Value, 0);
            uint den = BitConverter.ToUInt32(item.Value, 4);
            if (num == 0 || den == 0) return "0초";
            if (num >= den)
            {
                double sec = (double)num / den;
                return string.Format("{0:0.##}초", sec);
            }
            long recip = (long)Math.Round((double)den / num);
            return string.Format("1/{0}초", recip);
        }

        private static string FormatFNumber(PropertyItem item)
        {
            if (item.Value.Length < 8) return "";
            uint num = BitConverter.ToUInt32(item.Value, 0);
            uint den = BitConverter.ToUInt32(item.Value, 4);
            if (den == 0) return "";
            double f = (double)num / den;
            return string.Format("f/{0:0.0#}", f);
        }

        private static string FormatExposureBias(PropertyItem item)
        {
            if (item.Value.Length < 8) return "";
            int num = BitConverter.ToInt32(item.Value, 0);
            int den = BitConverter.ToInt32(item.Value, 4);
            if (den == 0) return "";
            double ev = (double)num / den;
            return string.Format("{0:+0.0;-0.0;0.0} EV", ev);
        }

        private static string FormatRational(PropertyItem item, string unit)
        {
            if (item.Value.Length < 8) return "";
            uint num = BitConverter.ToUInt32(item.Value, 0);
            uint den = BitConverter.ToUInt32(item.Value, 4);
            if (den == 0) return "";
            double val = (double)num / den;
            return string.Format("{0:0.#} {1}", val, unit);
        }

        private static string FormatExposureProgram(PropertyItem item)
        {
            if (item.Value.Length < 2) return "";
            ushort ep = BitConverter.ToUInt16(item.Value, 0);
            switch (ep)
            {
                case 1: return "수동 (M)";
                case 2: return "프로그램 자동 (P)";
                case 3: return "조리개 우선 (A / Av)";
                case 4: return "셔터 우선 (S / Tv)";
                case 5: return "크리에이티브 (심도)";
                case 6: return "액션 (고속 셔터)";
                case 7: return "인물 모드";
                case 8: return "풍경 모드";
                default: return "자동 (" + ep + ")";
            }
        }

        private static string FormatMeteringMode(PropertyItem item)
        {
            if (item.Value.Length < 2) return "";
            ushort mm = BitConverter.ToUInt16(item.Value, 0);
            switch (mm)
            {
                case 1: return "평균 측광 (Average)";
                case 2: return "중앙 중점 측광 (Center-weighted)";
                case 3: return "스팟 측광 (Spot)";
                case 4: return "다중 스팟 (Multi-spot)";
                case 5: return "다분할/평가 측광 (Multi-segment)";
                case 6: return "부분 측광 (Partial)";
                default: return "측광 (" + mm + ")";
            }
        }

        private static string FormatOrientation(PropertyItem item)
        {
            if (item.Value.Length < 2) return "";
            ushort ori = BitConverter.ToUInt16(item.Value, 0);
            switch (ori)
            {
                case 1: return "표준 (0°)";
                case 3: return "180° 회전";
                case 6: return "시계 방향 90° 회전";
                case 8: return "반시계 방향 90° 회전";
                default: return ori.ToString();
            }
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:0.##} KB ({1:N0} 바이트)", bytes / 1024.0, bytes);
            if (bytes < 1024 * 1024 * 1024) return string.Format("{0:0.##} MB ({1:N0} 바이트)", bytes / (1024.0 * 1024.0), bytes);
            return string.Format("{0:0.##} GB ({1:N0} 바이트)", bytes / (1024.0 * 1024.0 * 1024.0), bytes);
        }

        public static string GetAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0) return "";
            int gcd = GCD(width, height);
            int rx = width / gcd;
            int ry = height / gcd;
            double ratio = (double)width / height;
            if (Math.Abs(ratio - 16.0 / 9.0) < 0.02) return string.Format("16:9 ({0:0.##}:1)", ratio);
            if (Math.Abs(ratio - 4.0 / 3.0) < 0.02) return string.Format("4:3 ({0:0.##}:1)", ratio);
            if (Math.Abs(ratio - 21.0 / 9.0) < 0.02) return string.Format("21:9 ({0:0.##}:1)", ratio);
            if (Math.Abs(ratio - 1.0) < 0.01) return "1:1 (정사각형)";
            if (rx < 50 && ry < 50) return string.Format("{0}:{1} ({2:0.##}:1)", rx, ry, ratio);
            return string.Format("{0:0.##}:1", ratio);
        }

        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int t = b;
                b = a % b;
                a = t;
            }
            return a;
        }

        private static string DetectFormatName(string ext)
        {
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    return "JPEG 이미지";
                case ".png":
                    return "PNG (무손실 압축)";
                case ".webp":
                    return "WebP 이미지";
                case ".gif":
                    return "GIF 애니메이션";
                case ".bmp":
                    return "BMP 비트맵";
                default:
                    return ext.ToUpperInvariant().TrimStart('.') + " 이미지";
            }
        }

        private static string FormatPixelDepth(PixelFormat pf)
        {
            switch (pf)
            {
                case PixelFormat.Format32bppArgb:
                    return "32-bit ARGB (투명 채널 포함)";
                case PixelFormat.Format32bppRgb:
                    return "32-bit RGB";
                case PixelFormat.Format24bppRgb:
                    return "24-bit TrueColor RGB";
                case PixelFormat.Format8bppIndexed:
                    return "8-bit 인덱스 컬러 (256색)";
                case PixelFormat.Format16bppRgb565:
                    return "16-bit HighColor (RGB 565)";
                default:
                    return pf.ToString();
            }
        }
    }
}
