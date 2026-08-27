using ResoDrive.Core.Setup;

namespace ResoDrive.Core.Tests;

public sealed class SetupProfileTests
{
    private static SetupProfile Sample => LoadSample();

    [Fact]
    public void ShippedSampleCatalog_IsGenericAndValid()
    {
        var profile = Sample;

        Assert.Equal("example-nextcloud", profile.Id);
        var connection = Assert.IsType<WebDavConnectionDefinition>(profile.Connection);
        Assert.EndsWith(".example.invalid", connection.BaseUrl.Host, StringComparison.Ordinal);
        Assert.True(SetupProfileValidator.Validate(profile).IsValid);
    }

    [Fact]
    public void EmptyCatalog_IsAllowedOnlyForManualMode()
    {
        var catalog = new SetupProfileCatalog([], ProfileCatalogSource.None);

        Assert.Empty(catalog.Profiles);
        Assert.Throws<ArgumentException>(() =>
            new SetupProfileCatalog([], ProfileCatalogSource.AdjacentFile));
    }

    [Fact]
    public void CreateWebDavUri_EscapesUsernameAsOnePathSegment()
    {
        var endpoint = SetupProfileValidator.CreateWebDavUri(
            Sample,
            "captain/name@example.org");

        Assert.Equal(
            "https://cloud.example.invalid/remote.php/dav/files/captain%2Fname%40example.org",
            endpoint.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" user")]
    [InlineData("user\nname")]
    public void CreateWebDavUri_RejectsInvalidUsername(string username) =>
        Assert.Throws<ArgumentException>(() =>
            SetupProfileValidator.CreateWebDavUri(Sample, username));

    private static SetupProfile LoadSample()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "profiles.sample.json");
        var parsed = SetupProfileCatalogJson.Parse(File.ReadAllText(path));
        Assert.True(parsed.Succeeded, parsed.Error?.Message);
        return Assert.Single(parsed.Value!);
    }
}
