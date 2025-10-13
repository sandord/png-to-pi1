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
    public static async Task WriteAsPi1(Stream outputStream, Image<Byte4> image, ushort[] palette,
        byte transparencyPaletteIndex = 0, byte fallbackPaletteIndex = 1)
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

        var bitmapData = image.ToAtariStBitmap(
            offsetX: 0,
            offsetY: 0,
            image.Width,
            image.Height,
            palette,
            transparencyPaletteIndex,
            fallbackPaletteIndex);

        // Remove transparency entry from the palette.
        palette = palette.Index().Where(x => x.Index != transparencyPaletteIndex).Select(x => x.Item).ToArray();

        await WriteOutput(outputStream, palette, bitmapData);
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