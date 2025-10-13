namespace PngToPi1.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task MainConvert_ResultsInValidPi1File_WhenProvidingValidPngFile()
    {
        // Arrange.
        const string inputFile = "TestFiles/test-1-transparent+16+colors.png";
        const string outputFile = "test-output.pi1";

        var args = new List<string> { "convert", inputFile, outputFile };

        // Test.
        var result = await Program.Main(args.ToArray());

        // Assert.
        Assert.Equal(0, result);

        var expectedBytes = await File.ReadAllBytesAsync("TestFiles/test.pi1");
        var actualBytes = await File.ReadAllBytesAsync(outputFile);
        Assert.Equal(expectedBytes, actualBytes);
    }
}