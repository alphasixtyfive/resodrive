using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class AutostartCommandTests
{
    [Fact]
    public void Create_QuotesFullPathAndAddsBackgroundArgument()
    {
        var relativePath = Path.Combine("portable app", "rdrive.exe");

        var command = AutostartCommand.Create(relativePath);

        Assert.Equal(
            $"\"{Path.GetFullPath(relativePath)}\" --background",
            command);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsMissingPath(string? applicationPath) =>
        Assert.ThrowsAny<ArgumentException>(() => AutostartCommand.Create(applicationPath!));

    [Theory]
    [InlineData("--background", true)]
    [InlineData("--BACKGROUND", true)]
    [InlineData("background", false)]
    [InlineData(null, false)]
    public void IsBackgroundArgument_RecognizesOnlySupportedSwitch(
        string? argument,
        bool expected) =>
        Assert.Equal(expected, AutostartCommand.IsBackgroundArgument(argument));
}
