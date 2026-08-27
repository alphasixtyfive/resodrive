namespace ResoDrive.Windows.Tests;

public sealed class NetworkVolumeNameTests
{
    [Fact]
    public void CreateLocal_UsesTrimmedDriveName()
    {
        Assert.Equal("Archive", NetworkVolumeName.CreateLocal("  Archive  "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateLocal_OmitsEmptyDriveName(string displayName)
    {
        Assert.Null(NetworkVolumeName.CreateLocal(displayName));
    }

    [Fact]
    public void Create_UsesConnectionHostAndDriveName()
    {
        Assert.Equal(
            @"\\storage.example.com\Storage",
            NetworkVolumeName.Create("storage.example.com", "Storage", 'U'));
    }

    [Fact]
    public void Create_UsesStableFallbackForInvalidShareName()
    {
        Assert.Equal(
            @"\\storage.example.com\ResoDrive-S",
            NetworkVolumeName.Create("storage.example.com", "Bad/Name", 's'));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2001:db8::1")]
    [InlineData("invalid host")]
    public void Create_OmitsUnsupportedUncHosts(string? host)
    {
        Assert.Null(NetworkVolumeName.Create(host, "Storage", 'S'));
    }
}
