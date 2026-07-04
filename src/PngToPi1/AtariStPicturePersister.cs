using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PngToPi1.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PngToPi1;

public static class AtariStPicturePersister
{
    public static async Task WriteAsPi1(
        Stream outputStream,
        Image<Byte4> image,
        ushort[] finalPalette,
        int transparencyIndex = -1,
        List<(byte R, byte G, byte B)>? pngPaletteRgb = null,
        byte[]? pngPixelIndices = null,
        int pngWidth = 0,
        int pngHeight = 0)
    {
        const int width = 320;
        const int height = 200;
        const int paletteSize = 16;

        if (image.Width != width)
        {
            throw new ArgumentException($"Image width must be {width} pixels.", nameof(image));
        }

        if (image.Height != height)
        {
            throw new ArgumentException($"Image height must be {height} pixels.", nameof(image));
        }

        if (finalPalette.Length != paletteSize)
        {
            throw new ArgumentException($"Final palette must contain exactly {paletteSize} colors.", nameof(finalPalette));
        }

        // Index 0 of the final palette is the first non-transparent color; use it to replace
        // transparent pixels, since the transparency entry has been removed from the palette.
        var bitmapData = image.ToAtariStBitmap(
            width,
            height,
            finalPalette,
            transparencyIndex: transparencyIndex,
            transparencyReplacementIndex: 0,
            pngPaletteRgb: pngPaletteRgb,
            pngPixelIndices: pngPixelIndices,
            pngSourceWidth: pngWidth,
            pngSourceHeight: pngHeight);

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
