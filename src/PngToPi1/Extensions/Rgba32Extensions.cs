using System;
using SixLabors.ImageSharp.PixelFormats;

namespace PngToPi1.Extensions;

public static class Rgba32Extensions
{
    /// <summary>
    /// Returns the color of the specified RGBA32 value in the 512-color Atari ST palette format
    /// (-- -- -- -- -- R2 R1 R0 -- G2 G1 G0 -- B2 B1 B0). 
    /// </summary>
    public static ushort ToAtariStColor(this Rgba32 rgba32)
    {
        var r = (byte)Math.Min(7, Math.Floor(rgba32.R / 32f));
        var g = (byte)Math.Min(7, Math.Floor(rgba32.G / 32f));
        var b = (byte)Math.Min(7, Math.Floor(rgba32.B / 32f));

        return (ushort)(r << 8 | g << 4 | b);
    }
    
    public static int GetAtariStPaletteIndex(this Rgba32 rgba32, ushort[] palette, int fallbackPaletteIndex)
    {
        var index = Array.IndexOf(palette, rgba32.ToAtariStColor());
        return index == -1 ? fallbackPaletteIndex : index;
    }
}