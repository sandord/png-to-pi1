using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PngToPi1;

public static class AtariStPicturePersister
{
    public static async Task WriteAsPi1(Stream outputStream, Image<Byte4> image, ushort[] palette,
        byte translucencyPaletteIndex = 0, byte fallbackPaletteIndex = 1)
    {
        const int width = 320;
        const int height = 200;
        const int planeCount = 4;

        var paletteSize = (int)Math.Pow(2, planeCount);

        if (image.Width != width)
        {
            throw new ArgumentException($"Image width must be {width} pixels.", nameof(image));
        }

        if (image.Height != height)
        {
            throw new ArgumentException($"Image height must be {width} pixels.", nameof(image));
        }

        if (palette.Length != paletteSize)
        {
            throw new ArgumentException($"Palette size must be {paletteSize} colors.", nameof(palette));
        }

        var bitmapData = image.ToAtariStBitmap(
            offsetX: 0,
            offsetY: 0,
            image.Width,
            image.Height,
            palette,
            translucencyPaletteIndex,
            fallbackPaletteIndex);

        for (var i = 0; i < paletteSize; i++)
        {
            outputStream.WriteBigEndian16Bits(i < palette.Length ? palette[i] : (ushort)0);
        }

        await outputStream.WriteAsync(bitmapData);
    }
}