using System;
using System.IO;
using UnityEngine;

internal static class Bmp24Encoder
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;

    internal static byte[] Encode(Texture2D texture, int horizontalOffsetPixels = 0)
    {
        if (texture == null)
        {
            throw new ArgumentNullException(nameof(texture));
        }

        Color32[] pixels = texture.GetPixels32();
        int width = texture.width;
        int height = texture.height;
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
                    int sourceX = x - horizontalOffsetPixels;
                    Color32 pixel = sourceX >= 0 && sourceX < width
                        ? CompositeOnWhite(pixels[rowStart + sourceX])
                        : new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
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
