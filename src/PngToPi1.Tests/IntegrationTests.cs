using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace PngToPi1.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task ConvertPngToPi1_ResultsInValidPi1File_WhenProvidingValidPngFile()
    {
        // Arrange.
        const string inputFile = "TestFiles/test.png";
        const string outputFile = "test.pi1";

        // Test.
        var result = await Program.Main(["convert", inputFile, outputFile]);

        // Assert.
        Assert.Equal(0, result);
        Assert.True(File.Exists(outputFile));
        File.Delete(outputFile);
    }
}