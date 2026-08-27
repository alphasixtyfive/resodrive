namespace ResoDrive.Windows.Tests;

public sealed class RcloneErrorMessageTests
{
    [Fact]
    public void Clean_RemovesRclonePrefixAndRedactsCredentials()
    {
        const string message =
            "2026/08/27 11:02:46 CRITICAL: request https://user:password@example.test/x?token=abc failed";

        var result = RcloneErrorMessage.Clean(message);

        Assert.Equal("request https://***:***@example.test/x?token=*** failed", result);
    }

    [Fact]
    public void Clean_UsesLastLineAndRedactsAssignments()
    {
        var result = RcloneErrorMessage.Clean("noise\nERROR: password=secret connection failed");

        Assert.Equal("ERROR: password=*** connection failed", result);
    }

    [Fact]
    public void Clean_UsesFallbackForEmptyInput() =>
        Assert.Equal("Safe fallback", RcloneErrorMessage.Clean(null, "Safe fallback"));
}
