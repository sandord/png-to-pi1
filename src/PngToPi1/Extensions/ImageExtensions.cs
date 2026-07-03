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
        int originalTransparencyIndex = -1,
        byte[]? pngPixelIndices = null,
        int pngSourceWidth = 0,
        int pngSourceHeight = 0,
        ushort[]? finalPaletteSorted = null)
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

                        int colorIndex = 0;

                        // Determine Atari color for this pixel, preferring original PNG indices when available.
                        ushort atariColorForPixel = 0;

                        if (pngPixelIndices != null && pngSourceWidth == width && pngSourceHeight == height && pngPaletteRgb != null)
                        {
                            var origX = x + bit - offsetX;
                            var origY = y - offsetY;
                            var origIndex = pngPixelIndices[origY * pngSourceWidth + origX];

                            if (originalTransparencyIndex >= 0 && origIndex == originalTransparencyIndex)
                            {
                                // transparent -> replacement color
                                colorIndex = transparencyReplacementIndex;
                                atariColorForPixel = new Rgba32(0,0,0).ToAtariStColor();
                            }
                            else
                            {
                                var rgb = pngPaletteRgb[origIndex];
                                atariColorForPixel = new Rgba32(rgb.R, rgb.G, rgb.B).ToAtariStColor();
                            }
                        }
                        else if (rgba32.A == 0)
                        {
                            colorIndex = transparencyReplacementIndex;
                            atariColorForPixel = rgba32.ToAtariStColor();
                        }
                        else if (pngPaletteRgb != null)
                        {
                            // Try to find exact match in original PNG palette by RGB
                            var matchIndex = pngPaletteRgb.FindIndex(p => p.R == rgba32.R && p.G == rgba32.G && p.B == rgba32.B);
                            if (matchIndex >= 0)
                            {
                                if (originalTransparencyIndex >= 0 && matchIndex == originalTransparencyIndex)
                                {
                                    colorIndex = transparencyReplacementIndex;
                                }
                                else
                                {
                                    var orig = matchIndex;
                                    atariColorForPixel = new Rgba32(pngPaletteRgb[orig].R, pngPaletteRgb[orig].G, pngPaletteRgb[orig].B).ToAtariStColor();
                                }
                            }
                            else
                            {
                                // Nearest by RGB distance
                                var nearest = -1;
                                var bestDist = int.MaxValue;
                                for (var pi = 0; pi < pngPaletteRgb.Count; pi++)
                                {
                                    var p = pngPaletteRgb[pi];
                                    var dr = p.R - rgba32.R;
                                    var dg = p.G - rgba32.G;
                                    var db = p.B - rgba32.B;
                                    var dist = dr * dr + dg * dg + db * db;
                                    if (dist < bestDist)
                                    {
                                        bestDist = dist;
                                        nearest = pi;
                                    }
                                }

                                if (nearest >= 0)
                                {
                                    if (nearest == originalTransparencyIndex)
                                    {
                                        colorIndex = transparencyReplacementIndex;
                                    }
                                    else
                                    {
                                        var rgb = pngPaletteRgb[nearest];
                                        atariColorForPixel = new Rgba32(rgb.R, rgb.G, rgb.B).ToAtariStColor();
                                    }
                                }
                                else
                                {
                                    // fallback: use Atari lookup
                                    var idx = rgba32.GetAtariStPaletteIndex(palette, fallbackPaletteIndex, originalTransparencyIndex);
                                    colorIndex = originalTransparencyIndex >= 0 ? (idx == originalTransparencyIndex ? transparencyReplacementIndex : (idx > originalTransparencyIndex ? idx - 1 : idx)) : idx;
                                    atariColorForPixel = rgba32.ToAtariStColor();
                                }
                            }
                        }
                        else
                        {
                            // No png palette info, fallback to Atari lookup
                            var idx = rgba32.GetAtariStPaletteIndex(palette, fallbackPaletteIndex, originalTransparencyIndex);
                            colorIndex = originalTransparencyIndex >= 0 ? (idx == originalTransparencyIndex ? transparencyReplacementIndex : (idx > originalTransparencyIndex ? idx - 1 : idx)) : idx;
                            atariColorForPixel = rgba32.ToAtariStColor();
                        }

                        // If final canonical palette is provided, map the atari color to its index in that palette.
                        if (finalPaletteSorted != null)
                        {
                            var mapped = Array.IndexOf(finalPaletteSorted, atariColorForPixel);
                            if (mapped >= 0)
                            {
                                colorIndex = mapped;
                            }
                        }

                        if (y - offsetY == 1 && x - offsetX == 144)
                        {
                            Console.WriteLine($"DBG USE colorIndex={colorIndex} bit={bit} atari=0x{atariColorForPixel:X4}");
                        }

                        for (var p = 0; p < planeCount; p++)
                        {
                            planes[p] |= (ushort)(((colorIndex & (1 << p)) >> p) << (bitPlaneWordWidth - 1 - bit));
                        }
                    }

                    for (var p = 0; p < planeCount; p++)
                    {
                        bitmapData[outputOffset++] = (byte)(planes[p] >> 8);
                        bitmapData[outputOffset++] = (byte)(planes[p] & 0xff);
                    }

                    if (y - offsetY == 1 && x - offsetX == 144)
                    {
                        Console.WriteLine($"DBG BLOCK x={x} y={y} planes=[{planes[0]:X4},{planes[1]:X4},{planes[2]:X4},{planes[3]:X4}]");
                    }
                }
            }

            return bitmapData;
        }
}