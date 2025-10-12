using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PngToPi1;

public static class ImageExtensions
{
    /// <summary>
    /// Converts an image to an Atari ST bitmap. If <paramref name="width"/> is less than 16, the remaining bits are
    /// padded with 0. This means that the minimum width of the resulting bitmap is 16 pixels.
    /// </summary>
    public static byte[] ToAtariStBitmap(
        this Image<Byte4> image,
        int offsetX,
        int offsetY,
        int width,
        int height,
        ushort[] palette,
        byte translucencyIndex = 0,
        byte fallbackPaletteIndex = 1)
    {
        const byte bitPlaneWordWidth = 16;
        const byte planeCount = 4;

        // The physical width of the bitmap must be a multiple of 16.
        var physicalWidth = (ushort)((width + 15) & ~15);

        var bitmapData = new byte[height * (physicalWidth * planeCount / 8)];

        var rgba32 = new Rgba32();
        var outputOffset = 0;

        for (var y = offsetY; y < offsetY + height; y++)
        {
            for (var x = offsetX; x < offsetX + width; x += bitPlaneWordWidth)
            {
                var planes = new ushort[planeCount];

                for (var bit = 0; bit < Math.Min(physicalWidth, bitPlaneWordWidth); bit++)
                {
                    if (x + bit >= offsetX + width)
                    {
                        continue;
                    }

                    var pixel = image[x + bit, y];
                    pixel.ToRgba32(ref rgba32);

                    var colorIndex = rgba32.A == 0
                        ? translucencyIndex
                        : rgba32.GetAtariStPaletteIndex(palette, fallbackPaletteIndex);

                    for (var p = 0; p < planeCount; p++)
                    {
                        planes[p] |= (ushort)(((colorIndex & (byte)Math.Pow(2, p)) >> p)
                                              << (bitPlaneWordWidth - 1 - bit));
                    }
                }

                for (var p = 0; p < planeCount; p++)
                {
                    bitmapData[outputOffset++] = (byte)(planes[p] >> 8);
                    bitmapData[outputOffset++] = (byte)(planes[p] & 0xff);
                }
            }
        }

        return bitmapData;
    }
}