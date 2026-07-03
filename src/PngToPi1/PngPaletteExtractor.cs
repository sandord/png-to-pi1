using System;
using System.Collections.Generic;
using System.IO;

namespace PngToPi1;

public static class PngPaletteExtractor
{
    public static (List<(byte R, byte G, byte B)> Palette, int? TransparencyIndex, byte[]? PixelIndices, int Width, int Height) ExtractPalette(string pngFilePath)
    {
        using var fileStream = new FileStream(pngFilePath, FileMode.Open, FileAccess.Read);
        using var binaryReader = new BinaryReader(fileStream);

        var header = binaryReader.ReadBytes(8);

        if (!IsPngHeaderValid(header))
        {
            throw new InvalidOperationException("Not a valid PNG file.");
        }

        byte[]? plteData = null;
        byte[]? trnsData = null;
        byte[]? idatData = null;

        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = 0;

        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
        {
            var chunkLength = ReadBigEndianInt32(binaryReader);
            var chunkType = new string(binaryReader.ReadChars(4));

            if (chunkType == "PLTE")
            {
                plteData = binaryReader.ReadBytes(chunkLength);
            }
            else if (chunkType == "IHDR")
            {
                // IHDR: width(4) height(4) bit depth(1) color type(1) compression(1) filter(1) interlace(1)
                width = ReadBigEndianInt32(binaryReader);
                height = ReadBigEndianInt32(binaryReader);
                bitDepth = binaryReader.ReadByte();
                colorType = binaryReader.ReadByte();
                var compression = binaryReader.ReadByte();
                var filter = binaryReader.ReadByte();
                var interlace = binaryReader.ReadByte();
            }
            else if (chunkType == "IDAT")
            {
                var data = binaryReader.ReadBytes(chunkLength);
                if (idatData == null)
                {
                    idatData = data;
                }
                else
                {
                    var combined = new byte[idatData.Length + data.Length];
                    Buffer.BlockCopy(idatData, 0, combined, 0, idatData.Length);
                    Buffer.BlockCopy(data, 0, combined, idatData.Length, data.Length);
                    idatData = combined;
                }
            }
            else if (chunkType == "tRNS")
            {
                trnsData = binaryReader.ReadBytes(chunkLength);
            }
            else
            {
                // Skip other chunk data.
                binaryReader.BaseStream.Seek(chunkLength, SeekOrigin.Current);
            }

            // Skip CRC
            binaryReader.BaseStream.Seek(4, SeekOrigin.Current);

            if (plteData != null && trnsData != null && idatData != null)
            {
                break;
            }
        }

        if (plteData == null)
        {
            throw new InvalidOperationException("Palette not found (no PLTE chunk present).");
        }

        var palette = ParsePlteChunk(plteData);

        int? transparencyIndex = null;

        if (trnsData != null)
        {
            // tRNS for indexed-color PNG contains one byte per palette entry with alpha.
            // Find the first palette entry with alpha == 0 and consider that the transparency index.
            for (var i = 0; i < trnsData.Length; i++)
            {
                if (trnsData[i] == 0)
                {
                    transparencyIndex = i;
                    break;
                }
            }
        }

        // If we have IDAT data and this is an indexed-color PNG (color type 3) with bit depth 8,
        // attempt to decode the image pixels to obtain the original palette indices.
        byte[]? pixelIndices = null;

        if (idatData != null && colorType == 3 && bitDepth == 8 && width > 0 && height > 0)
        {
            try
            {
                Console.WriteLine($"DBG: IHDR width={width} height={height} colorType={colorType} bitDepth={bitDepth} idatLen={idatData.Length}");
                if (idatData.Length >= 2)
                {
                    Console.WriteLine($"DBG: idat header bytes: {idatData[0]:X2} {idatData[1]:X2} { (idatData.Length>2? idatData[2].ToString("X2") : "") }");
                }
                byte[] raw;
                try
                {
                    using var ms = new MemoryStream(idatData);
                    // Try decompressing the zlib-wrapped data directly.
                    using var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    ds.CopyTo(outMs);
                    raw = outMs.ToArray();
                    Console.WriteLine($"DBG: decompressed raw length = {raw.Length} (direct)");
                }
                catch (Exception)
                {
                    // Try stripping the zlib header (2 bytes) and Adler32 trailer (4 bytes) and decompressing raw deflate stream.
                    if (idatData.Length > 6)
                    {
                        var deflateRaw = new byte[idatData.Length - 6];
                        Buffer.BlockCopy(idatData, 2, deflateRaw, 0, deflateRaw.Length);
                        using var ms2 = new MemoryStream(deflateRaw);
                        using var ds2 = new System.IO.Compression.DeflateStream(ms2, System.IO.Compression.CompressionMode.Decompress);
                        using var outMs2 = new MemoryStream();
                        ds2.CopyTo(outMs2);
                        raw = outMs2.ToArray();
                        Console.WriteLine($"DBG: decompressed raw length = {raw.Length} (stripped zlib header/trailer)");
                    }
                    else
                    {
                        throw;
                    }
                }

                // Each scanline starts with a filter byte followed by width bytes (one index per pixel)
                var expectedRowSize = 1 + width;
                if (raw.Length >= expectedRowSize * height)
                {
                    pixelIndices = new byte[width * height];
                    var pos = 0;
                    for (var row = 0; row < height; row++)
                    {
                        var filterType = raw[pos++];
                        var scanline = new byte[width];
                        Array.Copy(raw, pos, scanline, 0, width);
                        pos += width;

                        // Apply PNG filter for the scanline
                        var recon = new byte[width];
                        switch (filterType)
                        {
                            case 0: // None
                                recon = scanline;
                                break;
                            case 1: // Sub
                                for (var i = 0; i < width; i++)
                                {
                                    var left = i >= 1 ? recon[i - 1] : (byte)0;
                                    recon[i] = (byte)((scanline[i] + left) & 0xFF);
                                }
                                break;
                            case 2: // Up
                                for (var i = 0; i < width; i++)
                                {
                                    var up = row >= 1 ? pixelIndices[(row - 1) * width + i] : (byte)0;
                                    recon[i] = (byte)((scanline[i] + up) & 0xFF);
                                }
                                break;
                            case 3: // Average
                                for (var i = 0; i < width; i++)
                                {
                                    var left = i >= 1 ? recon[i - 1] : (byte)0;
                                    var up = row >= 1 ? pixelIndices[(row - 1) * width + i] : (byte)0;
                                    recon[i] = (byte)((scanline[i] + ((left + up) >> 1)) & 0xFF);
                                }
                                break;
                            case 4: // Paeth
                                for (var i = 0; i < width; i++)
                                {
                                    var a = i >= 1 ? recon[i - 1] : (byte)0;
                                    var b = row >= 1 ? pixelIndices[(row - 1) * width + i] : (byte)0;
                                    var c = (i >= 1 && row >= 1) ? pixelIndices[(row - 1) * width + i - 1] : (byte)0;
                                    var p = a + b - c;
                                    var pa = Math.Abs(p - a);
                                    var pb = Math.Abs(p - b);
                                    var pc = Math.Abs(p - c);
                                    var pr = (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
                                    recon[i] = (byte)((scanline[i] + pr) & 0xFF);
                                }
                                break;
                            default:
                                recon = scanline;
                                break;
                        }

                        for (var i = 0; i < width; i++) pixelIndices[row * width + i] = recon[i];
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DBG: failed to decompress IDAT: {ex.GetType().Name}: {ex.Message}");
                pixelIndices = null;
            }
        }

        return (palette, transparencyIndex, pixelIndices, width, height);
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