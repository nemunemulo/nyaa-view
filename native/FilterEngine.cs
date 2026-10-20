using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NyaaView
{
    public enum FilterType
    {
        Off,
        Sharpen,
        Blur,
        Medium,
        UnsharpMask,
        MangaClean,
        Invert
    }

    public static class FilterEngine
    {
        // 3x3 컨볼루션 및 1:1 픽셀 LUT 고속 필터링 (LockBits + Marshal.Copy 기반)
        public static Bitmap ApplyFilter(Bitmap source, FilterType filter)
        {
            if (source == null || filter == FilterType.Off)
                return source != null ? (Bitmap)source.Clone() : null;

            int width = source.Width;
            int height = source.Height;

            Bitmap result = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            result.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            Bitmap src24 = source;
            bool disposeSrc24 = false;
            if (source.PixelFormat != PixelFormat.Format24bppRgb)
            {
                src24 = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(src24))
                {
                    g.DrawImage(source, 0, 0, width, height);
                }
                disposeSrc24 = true;
            }

            BitmapData srcData = src24.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                int stride = srcData.Stride;
                int byteCount = stride * height;
                byte[] srcBytes = new byte[byteCount];
                byte[] dstBytes = new byte[byteCount];

                Marshal.Copy(srcData.Scan0, srcBytes, 0, byteCount);
                src24.UnlockBits(srcData);
                srcData = null;

                if (disposeSrc24) { src24.Dispose(); disposeSrc24 = false; }

                // 1. 단일 픽셀 초고속 연산 (컨볼루션 이웃 참조 불필요)
                if (filter == FilterType.Invert)
                {
                    // 야간 반전 읽기 모드 (1ms 미만)
                    for (int i = 0; i < byteCount; i += 3)
                    {
                        dstBytes[i]     = (byte)(255 - srcBytes[i]);
                        dstBytes[i + 1] = (byte)(255 - srcBytes[i + 1]);
                        dstBytes[i + 2] = (byte)(255 - srcBytes[i + 2]);
                    }
                }
                else if (filter == FilterType.MangaClean)
                {
                    // 스캔본 만화 톤 보정 (레벨 보정 LUT: 누런 종이 순백화 & 선화 딥블랙화)
                    byte[] lut = new byte[256];
                    int blackPoint = 45;
                    int whitePoint = 215;
                    double range = whitePoint - blackPoint;

                    for (int i = 0; i < 256; i++)
                    {
                        if (i <= blackPoint) lut[i] = 0;
                        else if (i >= whitePoint) lut[i] = 255;
                        else
                        {
                            int mapped = (int)((i - blackPoint) * 255.0 / range + 0.5);
                            lut[i] = Clamp(mapped);
                        }
                    }

                    for (int i = 0; i < byteCount; i += 3)
                    {
                        dstBytes[i]     = lut[srcBytes[i]];
                        dstBytes[i + 1] = lut[srcBytes[i + 1]];
                        dstBytes[i + 2] = lut[srcBytes[i + 2]];
                    }
                }
                else
                {
                    // 2. 3x3 픽셀 컨볼루션 연산
                    float[,] kernel;
                    float factor = 1.0f;
                    float bias = 0.0f;

                    switch (filter)
                    {
                        case FilterType.Sharpen:
                            // 3x3 기본 선명화 커널
                            kernel = new float[,] {
                                {  0, -1,  0 },
                                { -1,  5, -1 },
                                {  0, -1,  0 }
                            };
                            break;

                        case FilterType.UnsharpMask:
                            // 꿀뷰 스타일 언샤프 마스크 커널 (부드럽게 + 선명하게)
                            kernel = new float[,] {
                                { -1, -2, -1 },
                                { -2, 28, -2 },
                                { -1, -2, -1 }
                            };
                            factor = 1.0f / 16.0f;
                            break;

                        case FilterType.Blur:
                            // 3x3 가우시안/박스 블러
                            kernel = new float[,] {
                                { 1, 2, 1 },
                                { 2, 4, 2 },
                                { 1, 2, 1 }
                            };
                            factor = 1.0f / 16.0f;
                            break;

                        case FilterType.Medium:
                        default:
                            // 3x3 중간값/스무딩 커널
                            kernel = new float[,] {
                                { 1, 1, 1 },
                                { 1, 2, 1 },
                                { 1, 1, 1 }
                            };
                            factor = 1.0f / 10.0f;
                            break;
                    }

                    // 고속 픽셀 컨볼루션 연산
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            float blue = 0, green = 0, red = 0;

                            for (int ky = -1; ky <= 1; ky++)
                            {
                                int py = Math.Min(height - 1, Math.Max(0, y + ky));
                                int rowOffset = py * stride;

                                for (int kx = -1; kx <= 1; kx++)
                                {
                                    int px = Math.Min(width - 1, Math.Max(0, x + kx));
                                    int idx = rowOffset + px * 3;
                                    float k = kernel[ky + 1, kx + 1];

                                    blue  += srcBytes[idx]     * k;
                                    green += srcBytes[idx + 1] * k;
                                    red   += srcBytes[idx + 2] * k;
                                }
                            }

                            int dstIdx = y * stride + x * 3;
                            dstBytes[dstIdx]     = Clamp((int)(blue  * factor + bias));
                            dstBytes[dstIdx + 1] = Clamp((int)(green * factor + bias));
                            dstBytes[dstIdx + 2] = Clamp((int)(red   * factor + bias));
                        }
                    }
                }

                Marshal.Copy(dstBytes, 0, dstData.Scan0, byteCount);
            }
            finally
            {
                if (srcData != null) src24.UnlockBits(srcData);
                if (disposeSrc24) src24.Dispose();
                result.UnlockBits(dstData);
            }

            return result;
        }

        private static byte Clamp(int val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }
    }
}
