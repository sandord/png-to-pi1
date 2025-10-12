using System;
using System.Collections.Generic;
using System.IO;

namespace PngToPi1;

public static class PngPaletteExtractor
{
    public static List<(byte R, byte G, byte B)> ExtractPalette(string pngFilePath)
    {
        using var fileStream = new FileStream(pngFilePath, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        var header = binaryReader.ReadBytes(8);

        if (!IsPngHeaderValid(header))
        {
            throw new InvalidOperationException("Not a valid PNG file.");
        }

        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
        {
            var chunkLength = ReadBigEndianInt32(binaryReader);
            var chunkType = new string(binaryReader.ReadChars(4));

            if (chunkType == "PLTE")
            {
                var paletteData = binaryReader.ReadBytes(chunkLength);
                var palette = ParsePlteChunk(paletteData);

                return palette;
            }

            // Skip current chunk data and CRC.
            binaryReader.BaseStream.Seek(chunkLength + 4, SeekOrigin.Current);
        }

        throw new InvalidOperationException("Palette not found (no PLTE chunk present).");
    }

    private static bool IsPngHeaderValid(byte[] header)
    {
        var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        return header.Length == 8 && header.AsSpan().SequenceEqual(pngSignature);
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static List<(byte R, byte G, byte B)> ParsePlteChunk(byte[] chunkData)
    {
        var palette = new List<(byte R, byte G, byte B)>();

        // Each color is 3 bytes: R, G, B
        for (var i = 0; i < chunkData.Length; i += 3)
        {
            palette.Add((chunkData[i], chunkData[i + 1], chunkData[i + 2]));
        }

        return palette;
    }
}