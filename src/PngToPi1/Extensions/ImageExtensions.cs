using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PngToPi1.Extensions;

public static class ImageExtensions
{
    /// <summary>
    /// Converts an image to interleaved 4-plane Atari ST low-resolution bitmap data.
    /// The physical width is rounded up to a multiple of 16 pixels; any padding bits are left as 0.
    /// </summary>
    /// <remarks>
    /// The pixel indices written into the bitplanes are indices into <paramref name="finalPalette"/>,
    /// i.e. the exact palette written to the PI1 file. When the original indexed-PNG pixel indices are
    /// available they are remapped directly (dropping the transparency entry); otherwise pixels are
    /// matched against the final palette by their Atari ST color.
    /// </remarks>
    public static byte[] ToAtariStBitmap(
        this Image<Byte4> image,
        int width,
        int height,
        ushort[] finalPalette,
        int transparencyIndex,
        byte transparencyReplacementIndex,
        List<(byte R, byte G, byte B)>? pngPaletteRgb = null,
        byte[]? pngPixelIndices = null,
        int pngSourceWidth = 0,
        int pngSourceHeight = 0)
    {
        const byte bitPlaneWordWidth = 16;
        const byte planeCount = 4;

        // The physical width of the bitmap must be a multiple of 16.
        var physicalWidth = (width + 15) & ~15;

        var bitmapData = new byte[height * (physicalWidth * planeCount / 8)];

        // When the source is an indexed PNG we can map each pixel's palette index straight through,
        // which is exact and preserves the palette ordering. Otherwise we fall back to color matching.
        var hasPngIndices = pngPixelIndices != null
            && pngSourceWidth == width
            && pngSourceHeight == height;

        var rgba32 = new Rgba32();
        var outputOffset = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x += bitPlaneWordWidth)
            {
                var planes = new ushort[planeCount];

                for (var bit = 0; bit < bitPlaneWordWidth; bit++)
                {
                    if (x + bit >= width)
                    {
                        continue;
                    }

                    int colorIndex;

                    if (hasPngIndices)
                    {
                        var originalIndex = pngPixelIndices![y * pngSourceWidth + (x + bit)];
                        colorIndex = MapOriginalIndex(originalIndex, transparencyIndex, transparencyReplacementIndex);
                    }
                    else
                    {
                        image[x + bit, y].ToRgba32(ref rgba32);

                        colorIndex = rgba32.A == 0
                            ? transparencyReplacementIndex
                            : NearestPaletteIndex(finalPalette, rgba32.ToAtariStColor());
                    }

                    for (var p = 0; p < planeCount; p++)
                    {
                        planes[p] |= (ushort)(((colorIndex >> p) & 1) << (bitPlaneWordWidth - 1 - bit));
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

    /// <summary>
    /// Maps an original PNG palette index to its index in the final palette, which has the
    /// transparency entry removed. Transparent pixels are replaced with a solid color index.
    /// </summary>
    private static int MapOriginalIndex(int originalIndex, int transparencyIndex, byte transparencyReplacementIndex)
    {
        if (transparencyIndex < 0)
        {
            return originalIndex;
        }

        if (originalIndex == transparencyIndex)
        {
            return transparencyReplacementIndex;
        }

        // Every entry after the removed transparency entry shifts down by one.
        return originalIndex < transparencyIndex ? originalIndex : originalIndex - 1;
    }

    /// <summary>
    /// Finds the index of the palette entry closest to <paramref name="target"/> (an Atari ST color).
    /// </summary>
    private static int NearestPaletteIndex(ushort[] palette, ushort target)
    {
        var exact = Array.IndexOf(palette, target);
        if (exact >= 0)
        {
            return exact;
        }

        var tr = (target >> 8) & 0x7;
        var tg = (target >> 4) & 0x7;
        var tb = target & 0x7;

        var nearest = 0;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < palette.Length; i++)
        {
            var dr = ((palette[i] >> 8) & 0x7) - tr;
            var dg = ((palette[i] >> 4) & 0x7) - tg;
            var db = (palette[i] & 0x7) - tb;
            var distance = dr * dr + dg * dg + db * db;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = i;
            }
        }

        return nearest;
    }
}
