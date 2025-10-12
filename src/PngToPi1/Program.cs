using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PngToPi1;
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
            new Command("convert", "Converts a 320x200 16 indexed color PNG file to an Atari ST PI1 file.");

        var inputFileArgument = new Argument<FileInfo>(name: "input")
        {
            Description = "The path to the input PNG file."
        };

        var outputFileArgument = new Argument<FileInfo>(name: "output")
        {
            Description = "The path to the output PI1 file."
        };

        buildCommand.Arguments.Add(inputFileArgument);
        buildCommand.Arguments.Add(outputFileArgument);

        buildCommand.SetAction(async result => await Convert(
            result.GetRequiredValue(inputFileArgument).FullName.Trim(),
            result.GetRequiredValue(outputFileArgument).FullName.Trim()));

        rootCommand.Subcommands.Add(buildCommand);

        var result = rootCommand.Parse(args);
        return await result.InvokeAsync();
    }
    
    private static async Task Convert(string inputFilePath, string outputFilePath)
    {
        var workingDirectory = Path.GetFullPath(Path.GetDirectoryName(inputFilePath) ?? ".");

        AnsiConsole.WriteLine($"Working directory is '{workingDirectory}'");
        AnsiConsole.WriteLine($"Reading input from '{Path.GetFileName(inputFilePath)}'");
        AnsiConsole.WriteLine();

        var palette = PngPaletteExtractor.ExtractPalette(inputFilePath)
            .Select(x => new Rgba32(x.R, x.G, x.B).ToAtariStColor())
            .ToArray();

        using var image = Image.Load<Byte4>(inputFilePath);

        AnsiConsole.WriteLine($"Writing output to '{outputFilePath}'");

        await using var outputStream = File.Open(outputFilePath, FileMode.Create);

        await AtariStPicturePersister.WriteAsPi1(outputStream, image, palette);
    }
}