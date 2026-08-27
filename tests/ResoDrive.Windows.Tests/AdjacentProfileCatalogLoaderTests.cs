using ResoDrive.Core.Setup;

namespace ResoDrive.Windows.Tests;

public sealed class AdjacentProfileCatalogLoaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"rdrive-profile-tests-{Guid.NewGuid():N}");

    [Fact]
    public void Load_MissingFileReturnsEmptyManualCatalog()
    {
        Directory.CreateDirectory(_directory);

        var catalog = AdjacentProfileCatalogLoader.Load(_directory);

        Assert.Equal(ProfileCatalogSource.None, catalog.Source);
        Assert.Null(catalog.Diagnostic);
        Assert.Empty(catalog.Profiles);
    }

    [Fact]
    public void Load_ValidFileReturnsAdjacentCatalog()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "profiles.json"), ValidJson);

        var catalog = AdjacentProfileCatalogLoader.Load(_directory);

        Assert.Equal(ProfileCatalogSource.AdjacentFile, catalog.Source);
        Assert.Null(catalog.Diagnostic);
        var connection = Assert.IsType<WebDavConnectionDefinition>(Assert.Single(catalog.Profiles).Connection);
        Assert.Equal("https://storage.example.org/", connection.BaseUrl.AbsoluteUri);
    }

    [Fact]
    public void Load_UnsafeFileReturnsEmptyManualCatalogWithDiagnostic()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, "profiles.json"),
            ValidJson.Replace("https://storage.example.org/", "https://user:password@attacker.example/", StringComparison.Ordinal));

        var catalog = AdjacentProfileCatalogLoader.Load(_directory);

        Assert.Equal(ProfileCatalogSource.None, catalog.Source);
        Assert.Contains("invalid", catalog.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(catalog.Profiles);
        Assert.Null(catalog.Find("custom"));
    }

    [Fact]
    public void Load_UserFileOverridesBundledCatalog()
    {
        var bundledDirectory = Path.Combine(_directory, "bundled");
        var userFile = Path.Combine(_directory, "user", "profiles.json");
        Directory.CreateDirectory(bundledDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(userFile)!);
        File.WriteAllText(Path.Combine(bundledDirectory, "profiles.json"), ValidJson);
        File.WriteAllText(
            userFile,
            ValidJson.Replace("Custom", "User profile", StringComparison.Ordinal));

        var catalog = AdjacentProfileCatalogLoader.Load(bundledDirectory, userFile);

        Assert.Equal(ProfileCatalogSource.UserFile, catalog.Source);
        Assert.Equal("User profile", Assert.Single(catalog.Profiles).DisplayName);
    }

    [Fact]
    public void Load_InvalidUserFileFallsBackToCurrentBundledCatalog()
    {
        var bundledDirectory = Path.Combine(_directory, "bundled");
        var userFile = Path.Combine(_directory, "user", "profiles.json");
        Directory.CreateDirectory(bundledDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(userFile)!);
        File.WriteAllText(Path.Combine(bundledDirectory, "profiles.json"), ValidJson);
        File.WriteAllText(userFile, "{ not-json }");

        var catalog = AdjacentProfileCatalogLoader.Load(bundledDirectory, userFile);

        Assert.Equal(ProfileCatalogSource.AdjacentFile, catalog.Source);
        Assert.Equal("Custom", Assert.Single(catalog.Profiles).DisplayName);
        Assert.Contains("invalid", catalog.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private const string ValidJson = """
        {
          "schemaVersion": 1,
          "profiles": [
            {
              "id": "custom",
              "displayName": "Custom",
              "description": "Managed deployment",
              "remoteName": "Storage",
              "serviceUri": "https://storage.example.org/",
              "webDavPathTemplate": "/dav/{username}",
              "vendor": "nextcloud",
              "defaultDriveLetter": "V",
              "mountArguments": []
            }
          ]
        }
        """;
}
