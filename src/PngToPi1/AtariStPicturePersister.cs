using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PngToPi1.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PngToPi1;

public static class AtariStPicturePersister
{
    public static async Task WriteAsPi1(Stream outputStream, Image<Byte4> image, ushort[] palette, ushort[] finalPalette, ushort[] finalPaletteSorted,
        byte transparencyPaletteIndex = 0, byte fallbackPaletteIndex = 1,
        System.Collections.Generic.List<(byte R, byte G, byte B)>? pngPaletteRgb = null,
        byte[]? pngPixelIndices = null,
        int pngWidth = 0,
        int pngHeight = 0)
    {
        const int width = 320;
        const int height = 200;
        const int planeCount = 4;

        var sourcePaletteSize = (int)Math.Pow(2, planeCount) + 1; // +1 for transparency.

        if (image.Width != width)
        {
            throw new ArgumentException($"Image width must be {width} pixels.", nameof(image));
        }

        if (image.Height != height)
        {
            throw new ArgumentException($"Image height must be {width} pixels.", nameof(image));
        }

        if (palette.Length != sourcePaletteSize)
        {
            throw new ArgumentException(
                $"Palette size must be {sourcePaletteSize} colors and one extra entry for transparency.", nameof(palette));
        }

        // Use the provided fallback palette index as the replacement for transparent pixels
        // and pass through the fallback index to the bitmap conversion logic.
        var bitmapData = image.ToAtariStBitmap(
            offsetX: 0,
            offsetY: 0,
            image.Width,
            image.Height,
            palette,
            // Always use the first color in the final palette as the replacement for transparent pixels.
            // The final palette is created by removing the transparency entry below, so index 0 in the
            // bitmap data corresponds to the first non-transparent color.
            transparencyReplacementIndex: 0,
            fallbackPaletteIndex: fallbackPaletteIndex,
            pngPaletteRgb: pngPaletteRgb,
            originalTransparencyIndex: transparencyPaletteIndex,
            pngPixelIndices: pngPixelIndices,
            pngSourceWidth: pngWidth,
            pngSourceHeight: pngHeight,
            finalPaletteSorted: finalPaletteSorted);

        // finalPaletteSorted already contains the canonical final palette (length 16)
        await WriteOutput(outputStream, finalPalette, bitmapData);
    }

    private static async Task WriteOutput(Stream outputStream, ushort[] palette, byte[] bitmapData)
    {
        // Resolution.
        outputStream.WriteBigEndian16Bits(0); // Low res.

        // Palette.
        for (ushort i = 0; i < palette.Length; i++)
        {
            outputStream.WriteBigEndian16Bits(palette[i]);
        }

        // Bitmap data.
        await outputStream.WriteAsync(bitmapData);

        // Color cycling information. Unused, so pad with 0s.
        for (var i = 0; i < 16; i++)
        {
            outputStream.WriteBigEndian16Bits(0);
        }

        if (outputStream.Length != 32066)
        {
            throw new InvalidOperationException("Output stream length is invalid.");
        }
    }
}