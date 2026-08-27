using ResoDrive.Core.Setup;

namespace ResoDrive.Core.Tests;

public sealed class SetupProfileCatalogJsonTests
{
    [Fact]
    public void Parse_AcceptsValidatedDeploymentOverrides()
    {
        var result = SetupProfileCatalogJson.Parse(ValidJson("https://storage.example.org/"));

        Assert.True(result.Succeeded);
        var profile = Assert.Single(result.Value!);
        var connection = Assert.IsType<WebDavConnectionDefinition>(profile.Connection);
        Assert.Equal("https://storage.example.org/", connection.BaseUrl.AbsoluteUri);
        Assert.Equal('V', profile.DefaultDriveLetter);
        Assert.Contains("--contimeout", profile.MountArguments);
    }

    [Theory]
    [InlineData("http://storage.example.org/")]
    [InlineData("https://user:password@storage.example.org/")]
    [InlineData("https://storage.example.org/?destination=other")]
    [InlineData("https://storage.example.org/#fragment")]
    [InlineData("https://storage.example.org/base/")]
    public void Parse_RejectsUnsafeServiceUris(string serviceUri)
    {
        var result = SetupProfileCatalogJson.Parse(ValidJson(serviceUri));

        Assert.False(result.Succeeded);
        Assert.Equal("profile.endpoint", result.Error?.Code);
    }

    [Theory]
    [InlineData("//attacker.example/{username}")]
    [InlineData("/dav/{username}?redirect=1")]
    [InlineData("/dav/{username}/../admin")]
    [InlineData("/dav/user")]
    [InlineData("/dav/{username}/{username}")]
    public void Parse_RejectsUnsafeWebDavTemplates(string template)
    {
        var json = ValidJson("https://storage.example.org/")
            .Replace("/dav/{username}", template, StringComparison.Ordinal);

        var result = SetupProfileCatalogJson.Parse(json);

        Assert.False(result.Succeeded);
        Assert.Equal("profile.webDavPath", result.Error?.Code);
    }

    [Fact]
    public void Parse_RejectsUnknownExecutionOrDownloadFields()
    {
        var json = ValidJson("https://storage.example.org/")
            .Replace("\"id\": \"custom\"", "\"id\": \"custom\", \"rcloneDownloadUrl\": \"https://attacker.example/rclone.exe\"", StringComparison.Ordinal);

        var result = SetupProfileCatalogJson.Parse(json);

        Assert.False(result.Succeeded);
        Assert.Equal("profiles.invalid_json", result.Error?.Code);
    }

    [Fact]
    public void Catalog_ReportsFileSourceAndDiagnostic()
    {
        var catalog = new SetupProfileCatalog(
            SetupProfileCatalogJson.Parse(ValidJson("https://storage.example.org/")).Value!,
            ProfileCatalogSource.AdjacentFile,
            @"C:\rdrive\profiles.json",
            "test diagnostic");

        Assert.Equal(ProfileCatalogSource.AdjacentFile, catalog.Source);
        Assert.Equal(@"C:\rdrive\profiles.json", catalog.SourcePath);
        Assert.Equal("test diagnostic", catalog.Diagnostic);
    }

    private static string ValidJson(string serviceUri) => $$"""
        {
          "schemaVersion": 2,
          "profiles": [
            {
              "id": "custom",
              "displayName": "Custom",
              "description": "Managed deployment",
              "defaultRemoteName": "Storage",
              "connection": {
                "type": "webdav",
                "baseUrl": "{{serviceUri}}",
                "pathTemplate": "/dav/{username}",
                "vendor": "nextcloud"
              },
              "defaultRemotePath": "",
              "defaultDriveLetter": "V",
              "startWithWindowsByDefault": true,
              "mountArguments": ["--contimeout", "30s"]
            }
          ]
        }
        """;
}
