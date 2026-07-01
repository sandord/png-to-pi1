using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PngToPi1.Extensions;

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
        byte transparencyReplacementIndex = 0,
        byte fallbackPaletteIndex = 1,
        System.Collections.Generic.List<(byte R, byte G, byte B)>? pngPaletteRgb = null,
        int originalTransparencyIndex = -1)
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

                    int colorIndex;

                    if (rgba32.A == 0)
                    {
                        colorIndex = transparencyReplacementIndex;
                    }
                    else if (pngPaletteRgb != null)
                    {
                        // Match pixel RGB against original PNG palette to get exact index.
                        var matchIndex = pngPaletteRgb.FindIndex(p => p.R == rgba32.R && p.G == rgba32.G && p.B == rgba32.B);

                        if (matchIndex >= 0)
                        {
                            // Map original PNG palette index to final palette index (account for removed transparency entry)
                            if (originalTransparencyIndex >= 0 && matchIndex != originalTransparencyIndex)
                            {
                                colorIndex = matchIndex < originalTransparencyIndex
                                    ? matchIndex
                                    : matchIndex - 1;
                            }
                            else if (matchIndex == originalTransparencyIndex)
                            {
                                colorIndex = transparencyReplacementIndex;
                            }
                            else
                            {
                                colorIndex = matchIndex;
                            }
                        }
                        else
                        {
                            // Fallback to Atari color lookup if exact RGB match not found.
                            colorIndex = rgba32.GetAtariStPaletteIndex(palette, fallbackPaletteIndex, transparencyReplacementIndex);
                        }
                    }
                    else
                    {
                        colorIndex = rgba32.GetAtariStPaletteIndex(palette, fallbackPaletteIndex, transparencyReplacementIndex);
                    }

                    for (var p = 0; p < planeCount; p++)
                    {
                        planes[p] |= (ushort)(((colorIndex & (1 << p)) >> p)
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