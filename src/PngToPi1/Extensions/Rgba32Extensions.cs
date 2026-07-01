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
    
    public static int GetAtariStPaletteIndex(this Rgba32 rgba32, ushort[] palette, int fallbackPaletteIndex, int transparencyPaletteIndex)
    {
        var target = rgba32.ToAtariStColor();
        
        // Prefer a matching palette index that is NOT the transparency index when possible.
        for (var i = 0; i < palette.Length; i++)
        {
            if (palette[i] == target && i != transparencyPaletteIndex)
            {
                return i;
            }
        }
        
        // If no non-transparency match was found, fall back to any match.
        var any = Array.IndexOf(palette, target);
        return any == -1 ? fallbackPaletteIndex : any;
    }
}