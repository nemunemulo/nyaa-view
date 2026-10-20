using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NyaaView
{
    // =========================================================================
    // 1. QOI (Quite OK Image format) 고속 디코더
    // =========================================================================
    public static class QoiDecoder
    {
        public static Bitmap Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 14) return null;
            if (bytes[0] != 'q' || bytes[1] != 'o' || bytes[2] != 'i' || bytes[3] != 'f') return null;

            int width = (bytes[4] << 24) | (bytes[5] << 16) | (bytes[6] << 8) | bytes[7];
            int height = (bytes[8] << 24) | (bytes[9] << 16) | (bytes[10] << 8) | bytes[11];
            int channels = bytes[12];
            if (width <= 0 || height <= 0 || (channels != 3 && channels != 4) || !ImageLoader.ValidateDimensions(width, height)) return null;

            Bitmap bmp = new Bitmap(width, height, channels == 4 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                byte[][] index = new byte[64][];
                for (int i = 0; i < 64; i++) index[i] = new byte[4];

                byte r = 0, g = 0, b = 0, a = 255;
                int totalPixels = width * height;
                int p = 14;
                int end = bytes.Length - 8;

                int stride = data.Stride;
                byte[] dst = new byte[stride * height];
                int pixelIndex = 0;

                while (pixelIndex < totalPixels && p < bytes.Length)
                {
                    byte b1 = bytes[p++];
                    if (b1 == 0xFE) // QOI_OP_RGB
                    {
                        r = bytes[p++];
                        g = bytes[p++];
                        b = bytes[p++];
                    }
                    else if (b1 == 0xFF) // QOI_OP_RGBA
                    {
                        r = bytes[p++];
                        g = bytes[p++];
                        b = bytes[p++];
                        a = bytes[p++];
                    }
                    else if ((b1 & 0xC0) == 0x00) // QOI_OP_INDEX
                    {
                        int idx = b1 & 0x3F;
                        r = index[idx][0];
                        g = index[idx][1];
                        b = index[idx][2];
                        a = index[idx][3];
                    }
                    else if ((b1 & 0xC0) == 0x40) // QOI_OP_DIFF
                    {
                        r = (byte)(r + ((b1 >> 4) & 0x03) - 2);
                        g = (byte)(g + ((b1 >> 2) & 0x03) - 2);
                        b = (byte)(b + (b1 & 0x03) - 2);
                    }
                    else if ((b1 & 0xC0) == 0x80) // QOI_OP_LUMA
                    {
                        byte b2 = bytes[p++];
                        int vg = (b1 & 0x3F) - 32;
                        r = (byte)(r + vg - 8 + ((b2 >> 4) & 0x0F));
                        g = (byte)(g + vg);
                        b = (byte)(b + vg - 8 + (b2 & 0x0F));
                    }
                    else if ((b1 & 0xC0) == 0xC0) // QOI_OP_RUN
                    {
                        int run = (b1 & 0x3F) + 1;
                        for (int i = 0; i < run && pixelIndex < totalPixels; i++)
                        {
                            WritePixel(dst, stride, pixelIndex++, width, r, g, b, a, channels);
                        }
                        continue;
                    }

                    int hash = ((r * 3) + (g * 5) + (b * 7) + (a * 11)) % 64;
                    index[hash][0] = r;
                    index[hash][1] = g;
                    index[hash][2] = b;
                    index[hash][3] = a;

                    WritePixel(dst, stride, pixelIndex++, width, r, g, b, a, channels);
                }

                Marshal.Copy(dst, 0, data.Scan0, dst.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private static void WritePixel(byte[] dst, int stride, int px, int width, byte r, byte g, byte b, byte a, int channels)
        {
            int x = px % width;
            int y = px / width;
            int offset = y * stride + x * (channels == 4 ? 4 : 3);
            dst[offset] = b;
            dst[offset + 1] = g;
            dst[offset + 2] = r;
            if (channels == 4) dst[offset + 3] = a;
        }
    }

    // =========================================================================
    // 2. TGA (Truevision Targa) 디코더 (비압축 및 RLE 지원)
    // =========================================================================
    public static class TgaDecoder
    {
        public static Bitmap Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 18) return null;

            int idLen = bytes[0];
            int colorMapType = bytes[1];
            int imageType = bytes[2];
            int width = BitConverter.ToInt16(bytes, 12);
            int height = BitConverter.ToInt16(bytes, 14);
            int bpp = bytes[16];
            byte descriptor = bytes[17];

            if (width <= 0 || height <= 0 || !ImageLoader.ValidateDimensions(width, height)) return null;
            if (colorMapType != 0) return null; // 인덱스 컬러맵은 지원 생략

            bool topDown = (descriptor & 0x20) != 0;
            bool isRle = (imageType == 10 || imageType == 11);
            bool isGray = (imageType == 3 || imageType == 11);
            bool isRgb = (imageType == 2 || imageType == 10);

            if (!isRgb && !isGray) return null;
            if (bpp != 8 && bpp != 24 && bpp != 32) return null;

            int bytesPerPixel = bpp / 8;
            int totalPixels = width * height;
            byte[] pixelData = new byte[totalPixels * bytesPerPixel];

            int offset = 18 + idLen;

            if (isRle)
            {
                int pOut = 0;
                while (pOut < pixelData.Length && offset < bytes.Length)
                {
                    byte packetHeader = bytes[offset++];
                    int count = (packetHeader & 0x7F) + 1;

                    if ((packetHeader & 0x80) != 0) // RLE 패킷
                    {
                        byte[] pixel = new byte[bytesPerPixel];
                        for (int k = 0; k < bytesPerPixel && offset < bytes.Length; k++)
                        {
                            pixel[k] = bytes[offset++];
                        }
                        for (int i = 0; i < count && pOut + bytesPerPixel <= pixelData.Length; i++)
                        {
                            for (int k = 0; k < bytesPerPixel; k++)
                            {
                                pixelData[pOut++] = pixel[k];
                            }
                        }
                    }
                    else // Raw 패킷
                    {
                        int bytesToCopy = count * bytesPerPixel;
                        for (int k = 0; k < bytesToCopy && offset < bytes.Length && pOut < pixelData.Length; k++)
                        {
                            pixelData[pOut++] = bytes[offset++];
                        }
                    }
                }
            }
            else
            {
                int bytesToRead = Math.Min(pixelData.Length, bytes.Length - offset);
                Buffer.BlockCopy(bytes, offset, pixelData, 0, bytesToRead);
            }

            PixelFormat format = (bpp == 32) ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            Bitmap bmp = new Bitmap(width, height, format);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, format);

            try
            {
                int dstStride = data.Stride;
                byte[] dst = new byte[dstStride * height];

                for (int y = 0; y < height; y++)
                {
                    int srcRow = topDown ? y : (height - 1 - y);
                    int srcOffset = srcRow * width * bytesPerPixel;
                    int dstOffset = y * dstStride;

                    for (int x = 0; x < width; x++)
                    {
                        int sIdx = srcOffset + x * bytesPerPixel;
                        int dIdx = dstOffset + x * (bpp == 32 ? 4 : 3);

                        if (isGray)
                        {
                            byte g = pixelData[sIdx];
                            dst[dIdx] = g;
                            dst[dIdx + 1] = g;
                            dst[dIdx + 2] = g;
                        }
                        else
                        {
                            // TGA는 BGR / BGRA 순서로 저장됨
                            dst[dIdx] = pixelData[sIdx];         // B
                            dst[dIdx + 1] = pixelData[sIdx + 1]; // G
                            dst[dIdx + 2] = pixelData[sIdx + 2]; // R
                            if (bpp == 32)
                            {
                                dst[dIdx + 3] = pixelData[sIdx + 3]; // A
                            }
                        }
                    }
                }

                Marshal.Copy(dst, 0, data.Scan0, dst.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }
    }

    // =========================================================================
    // 3. PSD (Adobe Photoshop) 플랫 합성본 고속 디코더
    // =========================================================================
    public static class PsdDecoder
    {
        public static Bitmap Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 30) return null;
            if (bytes[0] != '8' || bytes[1] != 'B' || bytes[2] != 'P' || bytes[3] != 'S') return null;

            int channels = ReadUInt16Big(bytes, 12);
            int height = ReadInt32Big(bytes, 14);
            int width = ReadInt32Big(bytes, 18);
            int depth = ReadUInt16Big(bytes, 22);
            int colorMode = ReadUInt16Big(bytes, 24);

            if (width <= 0 || height <= 0 || depth != 8 || !ImageLoader.ValidateDimensions(width, height)) return null;
            if (colorMode != 3 && colorMode != 1) return null; // RGB(3) 또는 Grayscale(1) 지원

            int pos = 26;
            // 1. Color Mode Data 스킵
            int colorModeLen = ReadInt32Big(bytes, pos);
            pos += 4 + colorModeLen;
            if (pos >= bytes.Length) return null;

            // 2. Image Resources 스킵
            int imgResLen = ReadInt32Big(bytes, pos);
            pos += 4 + imgResLen;
            if (pos >= bytes.Length) return null;

            // 3. Layer and Mask Info 스킵
            int layerLen = ReadInt32Big(bytes, pos);
            pos += 4 + layerLen;
            if (pos >= bytes.Length) return null;

            // 4. Image Data (최종 합성된 이미지 데이터)
            int compression = ReadUInt16Big(bytes, pos);
            pos += 2;

            int planeSize = width * height;
            int numChannelsToRead = Math.Min(channels, 4);
            byte[][] channelPlanes = new byte[numChannelsToRead][];
            for (int i = 0; i < numChannelsToRead; i++) channelPlanes[i] = new byte[planeSize];

            if (compression == 0) // Raw 비압축
            {
                for (int ch = 0; ch < numChannelsToRead; ch++)
                {
                    int copyLen = Math.Min(planeSize, bytes.Length - pos);
                    Buffer.BlockCopy(bytes, pos, channelPlanes[ch], 0, copyLen);
                    pos += planeSize;
                }
            }
            else if (compression == 1) // RLE PackBits 압축
            {
                int totalLines = height * channels;
                int byteCountsOffset = pos;
                pos += totalLines * 2; // RLE 행 바이트 수 테이블 스킵

                for (int ch = 0; ch < numChannelsToRead; ch++)
                {
                    int chWritten = 0;
                    byte[] plane = channelPlanes[ch];

                    while (chWritten < planeSize && pos < bytes.Length)
                    {
                        sbyte n = (sbyte)bytes[pos++];
                        if (n >= 0)
                        {
                            int count = n + 1;
                            for (int i = 0; i < count && chWritten < planeSize && pos < bytes.Length; i++)
                            {
                                plane[chWritten++] = bytes[pos++];
                            }
                        }
                        else if (n != -128)
                        {
                            int count = -n + 1;
                            byte val = (pos < bytes.Length) ? bytes[pos++] : (byte)0;
                            for (int i = 0; i < count && chWritten < planeSize; i++)
                            {
                                plane[chWritten++] = val;
                            }
                        }
                    }
                }
            }
            else
            {
                return null;
            }

            bool hasAlpha = (numChannelsToRead >= 4 && colorMode == 3);
            PixelFormat format = hasAlpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            Bitmap bmp = new Bitmap(width, height, format);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, format);

            try
            {
                int stride = data.Stride;
                byte[] dst = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * stride;
                    int planeRowOffset = y * width;

                    for (int x = 0; x < width; x++)
                    {
                        int pIdx = planeRowOffset + x;
                        int dIdx = rowOffset + x * (hasAlpha ? 4 : 3);

                        if (colorMode == 1) // Grayscale
                        {
                            byte g = channelPlanes[0][pIdx];
                            dst[dIdx] = g;
                            dst[dIdx + 1] = g;
                            dst[dIdx + 2] = g;
                        }
                        else // RGB
                        {
                            dst[dIdx] = channelPlanes[2][pIdx];     // B
                            dst[dIdx + 1] = channelPlanes[1][pIdx]; // G
                            dst[dIdx + 2] = channelPlanes[0][pIdx]; // R
                            if (hasAlpha)
                            {
                                dst[dIdx + 3] = channelPlanes[3][pIdx]; // A
                            }
                        }
                    }
                }

                Marshal.Copy(dst, 0, data.Scan0, dst.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private static int ReadInt32Big(byte[] b, int offset)
        {
            if (offset + 4 > b.Length) return 0;
            return (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
        }

        private static int ReadUInt16Big(byte[] b, int offset)
        {
            if (offset + 2 > b.Length) return 0;
            return (b[offset] << 8) | b[offset + 1];
        }
    }

    // =========================================================================
    // 4. DDS (DirectDraw Surface) 텍스처 디코더 (DXT1, DXT3, DXT5, Uncompressed)
    // =========================================================================
    public static class DdsDecoder
    {
        public static Bitmap Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 128) return null;
            if (bytes[0] != 'D' || bytes[1] != 'D' || bytes[2] != 'S' || bytes[3] != ' ') return null;

            int height = BitConverter.ToInt32(bytes, 12);
            int width = BitConverter.ToInt32(bytes, 16);
            if (width <= 0 || height <= 0 || !ImageLoader.ValidateDimensions(width, height)) return null;

            int pixelFormatFlags = BitConverter.ToInt32(bytes, 80);
            int fourCC = BitConverter.ToInt32(bytes, 84);
            int rgbBitCount = BitConverter.ToInt32(bytes, 88);

            // DXT1: 0x31545844 ('DXT1')
            // DXT3: 0x33545844 ('DXT3')
            // DXT5: 0x35545844 ('DXT5')
            bool isDxt1 = (fourCC == 0x31545844);
            bool isDxt3 = (fourCC == 0x33545844);
            bool isDxt5 = (fourCC == 0x35545844);

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = data.Stride;
                byte[] dst = new byte[stride * height];

                if (isDxt1 || isDxt3 || isDxt5)
                {
                    int blockSize = isDxt1 ? 8 : 16;
                    int blocksX = (width + 3) / 4;
                    int blocksY = (height + 3) / 4;
                    int offset = 128;

                    for (int by = 0; by < blocksY; by++)
                    {
                        for (int bx = 0; bx < blocksX; bx++)
                        {
                            if (offset + blockSize > bytes.Length) break;

                            byte[] alphaValues = null;
                            if (isDxt3)
                            {
                                alphaValues = new byte[16];
                                for (int i = 0; i < 8; i++)
                                {
                                    byte b = bytes[offset + i];
                                    alphaValues[i * 2] = (byte)((b & 0x0F) * 17);
                                    alphaValues[i * 2 + 1] = (byte)(((b >> 4) & 0x0F) * 17);
                                }
                                offset += 8;
                            }
                            else if (isDxt5)
                            {
                                alphaValues = DecodeDxt5Alpha(bytes, offset);
                                offset += 8;
                            }

                            // DXT 컬러 블록 디코딩
                            ushort c0 = BitConverter.ToUInt16(bytes, offset);
                            ushort c1 = BitConverter.ToUInt16(bytes, offset + 2);
                            uint lookup = BitConverter.ToUInt32(bytes, offset + 4);
                            offset += 8;

                            Color[] palette = new Color[4];
                            palette[0] = Rgb565ToColor(c0);
                            palette[1] = Rgb565ToColor(c1);

                            if (c0 > c1 || isDxt3 || isDxt5)
                            {
                                palette[2] = Color.FromArgb(
                                    (2 * palette[0].R + palette[1].R + 1) / 3,
                                    (2 * palette[0].G + palette[1].G + 1) / 3,
                                    (2 * palette[0].B + palette[1].B + 1) / 3);
                                palette[3] = Color.FromArgb(
                                    (palette[0].R + 2 * palette[1].R + 1) / 3,
                                    (palette[0].G + 2 * palette[1].G + 1) / 3,
                                    (palette[0].B + 2 * palette[1].B + 1) / 3);
                            }
                            else
                            {
                                palette[2] = Color.FromArgb(
                                    (palette[0].R + palette[1].R) / 2,
                                    (palette[0].G + palette[1].G) / 2,
                                    (palette[0].B + palette[1].B) / 2);
                                palette[3] = Color.FromArgb(0, 0, 0, 0); // 투명
                            }

                            for (int py = 0; py < 4; py++)
                            {
                                int y = by * 4 + py;
                                if (y >= height) continue;

                                for (int px = 0; px < 4; px++)
                                {
                                    int x = bx * 4 + px;
                                    if (x >= width) continue;

                                    int pixelIndex = py * 4 + px;
                                    int colorCode = (int)((lookup >> (pixelIndex * 2)) & 0x03);
                                    Color c = palette[colorCode];

                                    byte finalA = (isDxt3 || isDxt5) ? alphaValues[pixelIndex] : (byte)c.A;
                                    int dstOffset = y * stride + x * 4;
                                    dst[dstOffset] = c.B;
                                    dst[dstOffset + 1] = c.G;
                                    dst[dstOffset + 2] = c.R;
                                    dst[dstOffset + 3] = finalA;
                                }
                            }
                        }
                    }
                }
                else if (rgbBitCount == 32 && bytes.Length >= 128 + width * height * 4)
                {
                    // 비압축 32-bit RGBA
                    Buffer.BlockCopy(bytes, 128, dst, 0, width * height * 4);
                }
                else
                {
                    return null;
                }

                Marshal.Copy(dst, 0, data.Scan0, dst.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private static Color Rgb565ToColor(ushort c)
        {
            int r = ((c >> 11) & 0x1F) * 255 / 31;
            int g = ((c >> 5) & 0x3F) * 255 / 63;
            int b = (c & 0x1F) * 255 / 31;
            return Color.FromArgb(255, r, g, b);
        }

        private static byte[] DecodeDxt5Alpha(byte[] bytes, int offset)
        {
            byte[] alpha = new byte[16];
            byte a0 = bytes[offset];
            byte a1 = bytes[offset + 1];

            byte[] aPal = new byte[8];
            aPal[0] = a0;
            aPal[1] = a1;

            if (a0 > a1)
            {
                for (int i = 1; i <= 6; i++)
                    aPal[i + 1] = (byte)(((7 - i) * a0 + i * a1 + 3) / 7);
            }
            else
            {
                for (int i = 1; i <= 4; i++)
                    aPal[i + 1] = (byte)(((5 - i) * a0 + i * a1 + 2) / 5);
                aPal[6] = 0;
                aPal[7] = 255;
            }

            ulong bits = 0;
            for (int i = 0; i < 6; i++)
            {
                bits |= ((ulong)bytes[offset + 2 + i]) << (i * 8);
            }

            for (int i = 0; i < 16; i++)
            {
                int code = (int)((bits >> (i * 3)) & 0x07);
                alpha[i] = aPal[code];
            }

            return alpha;
        }
    }

    // =========================================================================
    // 5. Netpbm (PPM / PGM / PNM) 디코더 (P2, P3, P5, P6 지원)
    // =========================================================================
    public static class NetpbmDecoder
    {
        public static Bitmap Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 10) return null;
            if (bytes[0] != 'P') return null;

            char format = (char)bytes[1];
            if (format != '2' && format != '3' && format != '5' && format != '6') return null;

            int pos = 2;
            int width = ReadNextInt(bytes, ref pos);
            int height = ReadNextInt(bytes, ref pos);
            int maxVal = ReadNextInt(bytes, ref pos);

            if (width <= 0 || height <= 0 || maxVal <= 0 || !ImageLoader.ValidateDimensions(width, height)) return null;
            if (pos >= bytes.Length) return null;

            bool isColor = (format == '3' || format == '6');
            bool isBinary = (format == '5' || format == '6');

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            try
            {
                int stride = data.Stride;
                byte[] dst = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte r, g, b;
                        if (isBinary)
                        {
                            if (isColor)
                            {
                                r = (pos < bytes.Length) ? bytes[pos++] : (byte)0;
                                g = (pos < bytes.Length) ? bytes[pos++] : (byte)0;
                                b = (pos < bytes.Length) ? bytes[pos++] : (byte)0;
                            }
                            else
                            {
                                byte gray = (pos < bytes.Length) ? bytes[pos++] : (byte)0;
                                r = g = b = gray;
                            }
                        }
                        else
                        {
                            if (isColor)
                            {
                                r = (byte)ReadNextInt(bytes, ref pos);
                                g = (byte)ReadNextInt(bytes, ref pos);
                                b = (byte)ReadNextInt(bytes, ref pos);
                            }
                            else
                            {
                                byte gray = (byte)ReadNextInt(bytes, ref pos);
                                r = g = b = gray;
                            }
                        }

                        int dIdx = rowOffset + x * 3;
                        dst[dIdx] = b;
                        dst[dIdx + 1] = g;
                        dst[dIdx + 2] = r;
                    }
                }

                Marshal.Copy(dst, 0, data.Scan0, dst.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private static int ReadNextInt(byte[] bytes, ref int pos)
        {
            while (pos < bytes.Length)
            {
                // 공백 건너뛰기
                while (pos < bytes.Length && (bytes[pos] == ' ' || bytes[pos] == '\t' || bytes[pos] == '\r' || bytes[pos] == '\n'))
                {
                    pos++;
                }

                // 주석(#) 건너뛰기
                if (pos < bytes.Length && bytes[pos] == '#')
                {
                    while (pos < bytes.Length && bytes[pos] != '\n')
                    {
                        pos++;
                    }
                    continue;
                }

                break;
            }

            int val = 0;
            while (pos < bytes.Length && bytes[pos] >= '0' && bytes[pos] <= '9')
            {
                val = val * 10 + (bytes[pos] - '0');
                pos++;
            }

            return val;
        }
    }
}
