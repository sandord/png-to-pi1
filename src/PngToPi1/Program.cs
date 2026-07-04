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

        // Build the final 16-color palette by removing the transparency entry, preserving PNG order.
        var transparencyIndex = detectedTransparencyIndex ?? -1;
        var finalPalette = palette.Where((_, index) => index != transparencyIndex).ToArray();

        using var image = Image.Load<Byte4>(inputFilePath);

        AnsiConsole.WriteLine($"Writing output to '{outputFilePath}'");

        await using var outputStream = File.Open(outputFilePath, FileMode.Create);

        await AtariStPicturePersister.WriteAsPi1(
            outputStream,
            image,
            finalPalette,
            transparencyIndex: transparencyIndex,
            pngPaletteRgb: pngPalette,
            pngPixelIndices: pngPixelIndices,
            pngWidth: pngWidth,
            pngHeight: pngHeight);
    }
}