using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PngToPi1;
using PngToPi1.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Spectre.Console;

namespace PngToPi1;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand();

        var buildCommand =
            new Command("convert",
                "Converts a 320x200 indexed PNG file to an Atari ST PI1 file. The input file must have a 16-color palette with an extra entry for transparency at the beginning.");

        var inputFileArgument = new Argument<string>(name: "input")
        {
            Description = "The path to the input PNG file."
        };

        var outputFileArgument = new Argument<string>(name: "output")
        {
            Description = "The path to the output PI1 file."
        };

        buildCommand.Arguments.Add(inputFileArgument);
        buildCommand.Arguments.Add(outputFileArgument);

        buildCommand.SetAction(async result => await Convert(
            inputFilePath: result.GetRequiredValue(inputFileArgument).Trim(),
            outputFilePath: result.GetRequiredValue(outputFileArgument).Trim()));

        rootCommand.Subcommands.Add(buildCommand);

        var result = rootCommand.Parse(args);
        return await result.InvokeAsync();
    }

    private static async Task Convert(string inputFilePath, string outputFilePath)
    {
        AnsiConsole.WriteLine($"Reading input from '{inputFilePath}'");

        var (pngPalette, detectedTransparencyIndex, pngPixelIndices, pngWidth, pngHeight) = PngPaletteExtractor.ExtractPalette(inputFilePath);
        var palette = pngPalette.Select(x => new Rgba32(x.R, x.G, x.B).ToAtariStColor()).ToArray();

        // Build final palette (remove transparency entry) and a canonical sorted final palette
        var finalPalette = palette.Where((item, index) => index != (detectedTransparencyIndex ?? -1)).ToArray();
        var finalPaletteSorted = finalPalette.OrderBy(p => p).ToArray();

        // Diagnostic output for debugging palette / transparency mapping
        AnsiConsole.WriteLine($"Detected transparency index: {(detectedTransparencyIndex.HasValue ? detectedTransparencyIndex.Value.ToString() : "<none>")}");
        AnsiConsole.WriteLine($"Original PNG palette entries: {pngPalette.Count}");
        AnsiConsole.WriteLine($"Decoded PNG pixel indices available: {(pngPixelIndices != null ? "yes" : "no")}");
        if (pngPixelIndices != null)
        {
            AnsiConsole.WriteLine($"PNG dimensions from extractor: {pngWidth}x{pngHeight}");
        }
        AnsiConsole.WriteLine("Final canonical palette (sorted):");
        foreach (var c in finalPaletteSorted)
        {
            AnsiConsole.WriteLine($"  0x{c:X4}");
        }
        for (var i = 0; i < pngPalette.Count; i++)
        {
            var c = pngPalette[i];
            AnsiConsole.WriteLine($"  {i}: R={c.R} G={c.G} B={c.B} -> 0x{palette[i]:X4}");
        }

        using var image = Image.Load<Byte4>(inputFilePath);

        AnsiConsole.WriteLine($"Writing output to '{outputFilePath}'");

        await using var outputStream = File.Open(outputFilePath, FileMode.Create);

        // Use detected transparency index if available, otherwise default to 0.
        var transparencyIndex = (byte)(detectedTransparencyIndex ?? 0);

        // The fallback palette index should be the first non-transparency entry. If transparency is 0 use 1, otherwise use 0.
        var fallbackIndex = (byte)((detectedTransparencyIndex ?? 0) == 0 ? 1 : 0);

        await AtariStPicturePersister.WriteAsPi1(
            outputStream,
            image,
            palette,
            finalPalette,
            finalPaletteSorted,
            transparencyPaletteIndex: transparencyIndex,
            fallbackPaletteIndex: fallbackIndex,
            pngPaletteRgb: pngPalette,
            pngPixelIndices: pngPixelIndices,
            pngWidth: pngWidth,
            pngHeight: pngHeight);
    }
}