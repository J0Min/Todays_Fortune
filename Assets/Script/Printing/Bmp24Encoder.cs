using System;
using System.IO;
using UnityEngine;

public enum BixolonDitheringMode
{
    Sdk = 0,
    Threshold = 1,
    FloydSteinberg = 2,
    Bayer4x4 = 3,
    SierraLite = 4,
    Stucki = 5
}

internal static class Bmp24Encoder
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;
    private static readonly int[,] Bayer4x4Matrix =
    {
        { 0, 8, 2, 10 },
        { 12, 4, 14, 6 },
        { 3, 11, 1, 9 },
        { 15, 7, 13, 5 }
    };

    internal static byte[] Encode(
        Texture2D texture,
        int horizontalOffsetPixels,
        BixolonDitheringMode ditheringMode,
        int blackWhiteThreshold)
    {
        if (texture == null)
        {
            throw new ArgumentNullException(nameof(texture));
        }

        int width = texture.width;
        int height = texture.height;
        Color32[] pixels = CreateOutputPixels(
            texture,
            horizontalOffsetPixels,
            ditheringMode,
            Mathf.Clamp(blackWhiteThreshold, 0, 255));
        int rowSize = width * 3;
        int padding = (4 - rowSize % 4) % 4;
        int imageSize = (rowSize + padding) * height;
        int fileSize = FileHeaderSize + InfoHeaderSize + imageSize;

        using (MemoryStream stream = new MemoryStream(fileSize))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            WriteFileHeader(writer, fileSize);
            WriteInfoHeader(writer, width, height, imageSize);

            byte[] paddingBytes = new byte[padding];
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = pixels[rowStart + x];
                    writer.Write(pixel.b);
                    writer.Write(pixel.g);
                    writer.Write(pixel.r);
                }

                writer.Write(paddingBytes);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }

    private static Color32[] CreateOutputPixels(
        Texture2D texture,
        int horizontalOffsetPixels,
        BixolonDitheringMode ditheringMode,
        int blackWhiteThreshold)
    {
        int width = texture.width;
        int height = texture.height;
        Color32[] source = texture.GetPixels32();
        Color32[] output = new Color32[source.Length];

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int sourceX = x - horizontalOffsetPixels;
                output[rowStart + x] = sourceX >= 0 && sourceX < width
                    ? CompositeOnWhite(source[rowStart + sourceX])
                    : new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            }
        }

        switch (ditheringMode)
        {
            case BixolonDitheringMode.Threshold:
                ApplyThreshold(output, blackWhiteThreshold);
                break;
            case BixolonDitheringMode.FloydSteinberg:
                ApplyFloydSteinberg(output, width, height, blackWhiteThreshold);
                break;
            case BixolonDitheringMode.Bayer4x4:
                ApplyBayer4x4(output, width, height, blackWhiteThreshold);
                break;
            case BixolonDitheringMode.SierraLite:
                ApplySierraLite(output, width, height, blackWhiteThreshold);
                break;
            case BixolonDitheringMode.Stucki:
                ApplyStucki(output, width, height, blackWhiteThreshold);
                break;
        }

        return output;
    }

    private static void ApplyThreshold(Color32[] pixels, int threshold)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            byte value = GetLuminance(pixels[i]) >= threshold ? byte.MaxValue : byte.MinValue;
            pixels[i] = new Color32(value, value, value, byte.MaxValue);
        }
    }

    private static void ApplyFloydSteinberg(Color32[] pixels, int width, int height, int threshold)
    {
        float[] luminance = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            luminance[i] = GetLuminance(pixels[i]);
        }

        // Texture2D와 BMP는 아래쪽 행부터 저장되므로, 화면의 위에서 아래 방향으로 처리합니다.
        for (int y = height - 1; y >= 0; y--)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x;
                float oldValue = Mathf.Clamp(luminance[index], 0f, 255f);
                byte newValue = oldValue >= threshold ? byte.MaxValue : byte.MinValue;
                pixels[index] = new Color32(newValue, newValue, newValue, byte.MaxValue);
                float error = oldValue - newValue;

                AddError(luminance, width, height, x + 1, y, error * 7f / 16f);
                AddError(luminance, width, height, x - 1, y - 1, error * 3f / 16f);
                AddError(luminance, width, height, x, y - 1, error * 5f / 16f);
                AddError(luminance, width, height, x + 1, y - 1, error / 16f);
            }
        }
    }

    private static void ApplyBayer4x4(Color32[] pixels, int width, int height, int threshold)
    {
        float thresholdBias = 128f - threshold;
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x;
                float orderedThreshold = (Bayer4x4Matrix[y & 3, x & 3] + 0.5f) * 255f / 16f;
                byte value = GetLuminance(pixels[index]) + thresholdBias >= orderedThreshold
                    ? byte.MaxValue
                    : byte.MinValue;
                pixels[index] = new Color32(value, value, value, byte.MaxValue);
            }
        }
    }

    private static void ApplySierraLite(Color32[] pixels, int width, int height, int threshold)
    {
        float[] luminance = CreateLuminanceBuffer(pixels);

        for (int y = height - 1; y >= 0; y--)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x;
                float oldValue = Mathf.Clamp(luminance[index], 0f, 255f);
                byte newValue = oldValue >= threshold ? byte.MaxValue : byte.MinValue;
                pixels[index] = new Color32(newValue, newValue, newValue, byte.MaxValue);
                float error = oldValue - newValue;

                AddError(luminance, width, height, x + 1, y, error * 2f / 4f);
                AddError(luminance, width, height, x - 1, y - 1, error / 4f);
                AddError(luminance, width, height, x, y - 1, error / 4f);
            }
        }
    }

    private static void ApplyStucki(Color32[] pixels, int width, int height, int threshold)
    {
        float[] luminance = CreateLuminanceBuffer(pixels);

        for (int y = height - 1; y >= 0; y--)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x;
                float oldValue = Mathf.Clamp(luminance[index], 0f, 255f);
                byte newValue = oldValue >= threshold ? byte.MaxValue : byte.MinValue;
                pixels[index] = new Color32(newValue, newValue, newValue, byte.MaxValue);
                float error = oldValue - newValue;

                AddError(luminance, width, height, x + 1, y, error * 8f / 42f);
                AddError(luminance, width, height, x + 2, y, error * 4f / 42f);

                AddError(luminance, width, height, x - 2, y - 1, error * 2f / 42f);
                AddError(luminance, width, height, x - 1, y - 1, error * 4f / 42f);
                AddError(luminance, width, height, x, y - 1, error * 8f / 42f);
                AddError(luminance, width, height, x + 1, y - 1, error * 4f / 42f);
                AddError(luminance, width, height, x + 2, y - 1, error * 2f / 42f);

                AddError(luminance, width, height, x - 2, y - 2, error / 42f);
                AddError(luminance, width, height, x - 1, y - 2, error * 2f / 42f);
                AddError(luminance, width, height, x, y - 2, error * 4f / 42f);
                AddError(luminance, width, height, x + 1, y - 2, error * 2f / 42f);
                AddError(luminance, width, height, x + 2, y - 2, error / 42f);
            }
        }
    }

    private static float[] CreateLuminanceBuffer(Color32[] pixels)
    {
        float[] luminance = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            luminance[i] = GetLuminance(pixels[i]);
        }

        return luminance;
    }

    private static void AddError(
        float[] values,
        int width,
        int height,
        int x,
        int y,
        float error)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            values[y * width + x] += error;
        }
    }

    private static byte GetLuminance(Color32 pixel)
    {
        return (byte)((299 * pixel.r + 587 * pixel.g + 114 * pixel.b + 500) / 1000);
    }

    private static Color32 CompositeOnWhite(Color32 pixel)
    {
        if (pixel.a == byte.MaxValue)
        {
            return pixel;
        }

        int inverseAlpha = byte.MaxValue - pixel.a;
        return new Color32(
            (byte)((pixel.r * pixel.a + byte.MaxValue * inverseAlpha) / byte.MaxValue),
            (byte)((pixel.g * pixel.a + byte.MaxValue * inverseAlpha) / byte.MaxValue),
            (byte)((pixel.b * pixel.a + byte.MaxValue * inverseAlpha) / byte.MaxValue),
            byte.MaxValue);
    }

    private static void WriteFileHeader(BinaryWriter writer, int fileSize)
    {
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write(0);
        writer.Write(FileHeaderSize + InfoHeaderSize);
    }

    private static void WriteInfoHeader(BinaryWriter writer, int width, int height, int imageSize)
    {
        writer.Write(InfoHeaderSize);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
    }
}
