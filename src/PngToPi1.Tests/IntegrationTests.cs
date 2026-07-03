namespace PngToPi1.Tests;

public class IntegrationTests
{
    [Theory]
    [InlineData("TestFiles/test1.png", "TestFiles/test1.pi1", "test-output.pi1")]
    [InlineData("TestFiles/test2.png", "TestFiles/test2.pi1", "test-output.pi2")]
    public async Task MainConvert_ResultsInValidPi1File_WhenProvidingValidPngFile(string inputFile, string referenceFile, string outputFile)
    {
        // Arrange.
        var args = new List<string> { "convert", inputFile, outputFile };

        // Test.
        var result = await Program.Main(args.ToArray());

        // Assert.
        Assert.Equal(0, result);

        var expectedBytes = await File.ReadAllBytesAsync(referenceFile);
        var actualBytes = await File.ReadAllBytesAsync(outputFile);

        // Extract palette (first 32 bytes = 16 colors * 2 bytes each).
        var expectedPalette = expectedBytes.Skip(2).Take(32).ToArray();
        var actualPalette = actualBytes.Skip(2).Take(32).ToArray();

        // Mask to clear STE bits: bit 3, 7, and 11.
        const ushort steMask = unchecked((ushort)~0x0888);

        // Compare palette colors with STE bits masked out.
        for (var i = 0; i < expectedPalette.Length; i += 2)
        {
            var expectedColor = (ushort)(expectedPalette[i] << 8 | expectedPalette[i + 1]);
            var actualColor = (ushort)(actualPalette[i] << 8 | actualPalette[i + 1]);

            Assert.Equal(expectedColor & steMask, actualColor & steMask);
        }

        // Compare the rest of the file.
        var expectedImageData = expectedBytes.Skip(34).ToArray();
        var actualImageData = actualBytes.Skip(34).ToArray();
        Assert.Equal(expectedImageData, actualImageData);
    }
}