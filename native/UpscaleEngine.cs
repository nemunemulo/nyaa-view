using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NyaaView
{
    // 업스케일링 모드
    public enum UpscaleMode
    {
        Off = 0,        // 꺼짐 (기본값)
        SmartEdge = 1,  // 스마트 엣지 업스케일 (Lanczos/Catmull-Rom + CAS 대비 선명도 복원 - FSR 스타일)
        MangaLine = 2   // 2D 만화 펜선 복원 (Anime4K 라인 복원 & 안티앨리어싱 - 흑백/출판 만화 특화)
    }

    public static class UpscaleEngine
    {
        // 업스케일링 적용 기준 (너무 큰 원본은 불필요한 연산 방지를 위해 제외)
        // 가로 1920px 또는 세로 2560px 미만의 저해상도 이미지에 2배 업스케일 적용
        public const int MaxAutoUpscaleWidth = 1920;
        public const int MaxAutoUpscaleHeight = 2560;

        public static bool ShouldUpscale(Bitmap source)
        {
            if (source == null) return false;
            return (source.Width <= MaxAutoUpscaleWidth && source.Height <= MaxAutoUpscaleHeight);
        }

        public static Bitmap ApplyUpscale(Bitmap source, UpscaleMode mode)
        {
            if (source == null || mode == UpscaleMode.Off)
                return source;

            // 이미 고해상도(2K/4K 이상)인 이미지는 원본 그대로 반환
            if (!ShouldUpscale(source))
                return source;

            try
            {
                switch (mode)
                {
                    case UpscaleMode.SmartEdge:
                        return UpscaleSmartEdge(source);
                    case UpscaleMode.MangaLine:
                        return UpscaleMangaLine(source);
                    default:
                        return source;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpscaleEngine failure: " + ex.Message);
                return source;
            }
        }

        // =========================================================================
        // 1. 스마트 엣지 업스케일러 (Lanczos / Catmull-Rom + Contrast-Adaptive Sharpening)
        // AMD FSR 1.0 (EASU + RCAS) 핵심 원리를 C# 네이티브 픽셀 파이프라인으로 구현
        // =========================================================================
        private static Bitmap UpscaleSmartEdge(Bitmap source)
        {
            int srcW = source.Width;
            int srcH = source.Height;
            int dstW = srcW * 2;
            int dstH = srcH * 2;

            // 1단계: 고품질 쌍입방(Bicubic) / 스플라인 2배 해상도 확장
            Bitmap upscaled = new Bitmap(dstW, dstH, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(upscaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, dstW, dstH), 0, 0, srcW, srcH, GraphicsUnit.Pixel);
            }

            // 2단계: 대비 적응형 선명화 (CAS - Contrast Adaptive Sharpening)
            // 3x3 로컬 대비를 감지하여 엣지 방향으로만 적응형 가중치를 주어 링잉(헤일로) 없이 선명화
            BitmapData srcData = upscaled.LockBits(new Rectangle(0, 0, dstW, dstH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int byteCount = Math.Abs(srcData.Stride) * dstH;
            byte[] srcBytes = new byte[byteCount];
            byte[] dstBytes = new byte[byteCount];
            Marshal.Copy(srcData.Scan0, srcBytes, 0, byteCount);
            upscaled.UnlockBits(srcData);
            upscaled.Dispose();

            int stride = srcData.Stride;

            Parallel.For(0, dstH, y =>
            {
                int rowOffset = y * stride;
                int yT = (y > 0) ? (y - 1) * stride : rowOffset;
                int yB = (y < dstH - 1) ? (y + 1) * stride : rowOffset;

                for (int x = 0; x < dstW; x++)
                {
                    int xM = x * 4;
                    int xL = (x > 0 ? x - 1 : 0) * 4;
                    int xR = (x < dstW - 1 ? x + 1 : dstW - 1) * 4;

                    int idxM = rowOffset + xM;
                    int idxT = yT + xM;
                    int idxB = yB + xM;
                    int idxL = rowOffset + xL;
                    int idxR = rowOffset + xR;

                    // Center, Top, Bottom, Left, Right 샘플링 (BGRA)
                    byte bM = srcBytes[idxM], gM = srcBytes[idxM + 1], rM = srcBytes[idxM + 2], aM = srcBytes[idxM + 3];
                    byte bT = srcBytes[idxT], gT = srcBytes[idxT + 1], rT = srcBytes[idxT + 2];
                    byte bB = srcBytes[idxB], gB = srcBytes[idxB + 1], rB = srcBytes[idxB + 2];
                    byte bL = srcBytes[idxL], gL = srcBytes[idxL + 1], rL = srcBytes[idxL + 2];
                    byte bR = srcBytes[idxR], gR = srcBytes[idxR + 1], rR = srcBytes[idxR + 2];

                    // 휘도(Luminance) 계산
                    float yValM = (rM * 299 + gM * 587 + bM * 114) / 1000f;
                    float yValT = (rT * 299 + gT * 587 + bT * 114) / 1000f;
                    float yValB = (rB * 299 + gB * 587 + bB * 114) / 1000f;
                    float yValL = (rL * 299 + gL * 587 + bL * 114) / 1000f;
                    float yValR = (rR * 299 + gR * 587 + bR * 114) / 1000f;

                    // 로컬 최소/최대 휘도
                    float minL = Math.Min(yValM, Math.Min(Math.Min(yValT, yValB), Math.Min(yValL, yValR)));
                    float maxL = Math.Max(yValM, Math.Max(Math.Max(yValT, yValB), Math.Max(yValL, yValR)));

                    // CAS 가중치 계산: 대비가 강한 엣지는 선명하게, 평평하거나 극한 대비는 링잉 억제
                    float contrast = Math.Min(minL, 255f - maxL) / (maxL + 0.1f);
                    float amp = (float)Math.Sqrt(Math.Max(0f, Math.Min(1.0f, contrast)));
                    float w = -amp * 0.22f; // 가중치 계수 (-0.22 ~ 0)

                    float denom = 4.0f * w + 1.0f;

                    // RGB 채널별 적응형 합성
                    int outB = (int)((w * (bT + bB + bL + bR) + bM) / denom + 0.5f);
                    int outG = (int)((w * (gT + gB + gL + gR) + gM) / denom + 0.5f);
                    int outR = (int)((w * (rT + rB + rL + rR) + rM) / denom + 0.5f);

                    dstBytes[idxM] = (byte)(outB < 0 ? 0 : (outB > 255 ? 255 : outB));
                    dstBytes[idxM + 1] = (byte)(outG < 0 ? 0 : (outG > 255 ? 255 : outG));
                    dstBytes[idxM + 2] = (byte)(outR < 0 ? 0 : (outR > 255 ? 255 : outR));
                    dstBytes[idxM + 3] = aM;
                }
            });

            Bitmap result = new Bitmap(dstW, dstH, PixelFormat.Format32bppArgb);
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, dstW, dstH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(dstBytes, 0, dstData.Scan0, byteCount);
            result.UnlockBits(dstData);

            return result;
        }

        // =========================================================================
        // 2. 2D 만화 펜선 복원 업스케일러 (Anime4K Line Thinning & Anti-Aliasing)
        // 만화의 흑백/컬러 펜선 외곽선 기울기를 계산하여 펜선을 벡터처럼 매끄럽고 또렷하게 복원
        // =========================================================================
        private static Bitmap UpscaleMangaLine(Bitmap source)
        {
            int srcW = source.Width;
            int srcH = source.Height;
            int dstW = srcW * 2;
            int dstH = srcH * 2;

            // 1단계: 2배 스케일 초기화
            Bitmap upscaled = new Bitmap(dstW, dstH, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(upscaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, dstW, dstH), 0, 0, srcW, srcH, GraphicsUnit.Pixel);
            }

            // 2단계: 소벨(Sobel) 기울기 분석 및 펜선 외곽선 당김(Thinning) & 안티앨리어싱
            BitmapData srcData = upscaled.LockBits(new Rectangle(0, 0, dstW, dstH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int byteCount = Math.Abs(srcData.Stride) * dstH;
            byte[] srcBytes = new byte[byteCount];
            byte[] dstBytes = new byte[byteCount];
            Marshal.Copy(srcData.Scan0, srcBytes, 0, byteCount);
            upscaled.UnlockBits(srcData);
            upscaled.Dispose();

            int stride = srcData.Stride;

            Parallel.For(0, dstH, y =>
            {
                int rowOffset = y * stride;
                int yT = (y > 0) ? (y - 1) * stride : rowOffset;
                int yB = (y < dstH - 1) ? (y + 1) * stride : rowOffset;

                for (int x = 0; x < dstW; x++)
                {
                    int xM = x * 4;
                    int xL = (x > 0 ? x - 1 : 0) * 4;
                    int xR = (x < dstW - 1 ? x + 1 : dstW - 1) * 4;

                    int idxM = rowOffset + xM;
                    byte bM = srcBytes[idxM], gM = srcBytes[idxM + 1], rM = srcBytes[idxM + 2], aM = srcBytes[idxM + 3];

                    // 3x3 픽셀 휘도
                    float lM = (rM * 299 + gM * 587 + bM * 114) / 1000f;
                    float lT = (srcBytes[yT + xM + 2] * 299 + srcBytes[yT + xM + 1] * 587 + srcBytes[yT + xM] * 114) / 1000f;
                    float lB = (srcBytes[yB + xM + 2] * 299 + srcBytes[yB + xM + 1] * 587 + srcBytes[yB + xM] * 114) / 1000f;
                    float lL = (srcBytes[rowOffset + xL + 2] * 299 + srcBytes[rowOffset + xL + 1] * 587 + srcBytes[rowOffset + xL] * 114) / 1000f;
                    float lR = (srcBytes[rowOffset + xR + 2] * 299 + srcBytes[rowOffset + xR + 1] * 587 + srcBytes[rowOffset + xR] * 114) / 1000f;

                    // 대각선 코너 휘도
                    float lTL = (srcBytes[yT + xL + 2] * 299 + srcBytes[yT + xL + 1] * 587 + srcBytes[yT + xL] * 114) / 1000f;
                    float lTR = (srcBytes[yT + xR + 2] * 299 + srcBytes[yT + xR + 1] * 587 + srcBytes[yT + xR] * 114) / 1000f;
                    float lBL = (srcBytes[yB + xL + 2] * 299 + srcBytes[yB + xL + 1] * 587 + srcBytes[yB + xL] * 114) / 1000f;
                    float lBR = (srcBytes[yB + xR + 2] * 299 + srcBytes[yB + xR + 1] * 587 + srcBytes[yB + xR] * 114) / 1000f;

                    // Sobel 경사도(Gradient) 벡터 계산: 선의 진행 방향과 법선(수직) 탐지
                    float gx = (lTR + 2f * lR + lBR) - (lTL + 2f * lL + lBL);
                    float gy = (lBL + 2f * lB + lBR) - (lTL + 2f * lT + lTR);
                    float gMag = (float)Math.Sqrt(gx * gx + gy * gy);

                    // 펜선 경계 검출 (임계값 18 이상인 엣지 영역)
                    if (gMag > 18.0f)
                    {
                        // 엣지 법선 단위 벡터 (어두운 선 내부 방향)
                        float nx = -gx / gMag;
                        float ny = -gy / gMag;

                        // 법선 방향의 인접 픽셀 위치
                        int targetX = Math.Max(0, Math.Min(dstW - 1, x + (int)Math.Round(nx)));
                        int targetY = Math.Max(0, Math.Min(dstH - 1, y + (int)Math.Round(ny)));

                        int targetIdx = (targetY * stride) + (targetX * 4);
                        byte tb = srcBytes[targetIdx], tg = srcBytes[targetIdx + 1], tr = srcBytes[targetIdx + 2];

                        // 펜선의 흐릿한 외곽 픽셀이면 더 어두운 잉크 쪽으로 당김 (Thinning & Sharpness)
                        float blend = 0.42f;
                        int finalB = (int)(bM * (1f - blend) + tb * blend + 0.5f);
                        int finalG = (int)(gM * (1f - blend) + tg * blend + 0.5f);
                        int finalR = (int)(rM * (1f - blend) + tr * blend + 0.5f);

                        dstBytes[idxM] = (byte)(finalB < 0 ? 0 : (finalB > 255 ? 255 : finalB));
                        dstBytes[idxM + 1] = (byte)(finalG < 0 ? 0 : (finalG > 255 ? 255 : finalG));
                        dstBytes[idxM + 2] = (byte)(finalR < 0 ? 0 : (finalR > 255 ? 255 : finalR));
                        dstBytes[idxM + 3] = aM;
                    }
                    else
                    {
                        // 엣지가 아닌 평탄한 영역은 깨끗하게 원본 유지
                        dstBytes[idxM] = bM;
                        dstBytes[idxM + 1] = gM;
                        dstBytes[idxM + 2] = rM;
                        dstBytes[idxM + 3] = aM;
                    }
                }
            });

            Bitmap result = new Bitmap(dstW, dstH, PixelFormat.Format32bppArgb);
            BitmapData dstData = result.LockBits(new Rectangle(0, 0, dstW, dstH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(dstBytes, 0, dstData.Scan0, byteCount);
            result.UnlockBits(dstData);

            return result;
        }
    }
}
