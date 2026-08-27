using ResoDrive.Core.Setup;

namespace ResoDrive.Core.Tests;

public sealed class ProfileSetupPlanTests
{
    [Fact]
    public void CreateMount_BuildsManagedMountSettings()
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "vessel",
                Username = "captain@example.org",
                DisplayName = "Captain's files",
                RemotePath = "/Documents/",
                DriveLetter = 'V',
                NetworkMode = true,
                AutoMountOnApplicationStart = true,
                StartWithWindows = true
            }, Catalog, "Storage");

        Assert.True(result.Succeeded);
        var mount = Assert.IsType<ResoDrive.Core.Settings.MountSettings>(result.Value);
        Assert.Equal("/Documents", mount.RemotePath);
        Assert.Equal('V', mount.Target.DriveLetter);
        Assert.Equal("OnApplicationStart", mount.AutoMount);
        Assert.Equal("storage.example.org", mount.ConnectionHost);
        Assert.Equal("WebDAV", mount.ConnectionType);
        Assert.Contains("--contimeout", mount.Arguments);
        Assert.Contains("--network-mode", mount.Arguments);
    }

    [Fact]
    public void Create_RejectsUnknownProfile()
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "unknown",
                Username = "user",
                DisplayName = "Storage",
                DriveLetter = 'U'
            },
            Catalog,
            "Storage");

        Assert.False(result.Succeeded);
        Assert.Equal("setup.profile", result.Error?.Code);
    }

    [Fact]
    public void CreateMount_UsesExplicitArgumentsInsteadOfProfileDefaults()
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "vessel",
                Username = "user",
                DisplayName = "Storage",
                DriveLetter = 'U',
                MountArguments = ["--poll-interval=30s"]
            }, Catalog, "Storage");

        var mount = Assert.IsType<ResoDrive.Core.Settings.MountSettings>(result.Value);
        Assert.Equal(["--poll-interval=30s"], mount.Arguments);
        Assert.DoesNotContain("--contimeout", mount.Arguments);
    }

    [Fact]
    public void CreateMount_AllowsExplicitlyClearingProfileArguments()
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "vessel",
                Username = "user",
                DisplayName = "Storage",
                DriveLetter = 'U',
                MountArguments = []
            }, Catalog, "Storage");

        Assert.Empty(Assert.IsType<ResoDrive.Core.Settings.MountSettings>(result.Value).Arguments);
    }

    [Fact]
    public void CreateMount_RejectsUnsafeArgumentOverrides()
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "default",
                Username = "user",
                DisplayName = "Storage",
                DriveLetter = 'U',
                MountArguments = ["--password-command", "bad.exe"]
            }, Catalog, "Storage");

        Assert.False(result.Succeeded);
        Assert.Equal("arguments.externalCommand", result.Error?.Code);
    }

    [Fact]
    public void Create_RejectsRemoteTraversal()
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "default",
                Username = "user",
                DisplayName = "Storage",
                RemotePath = "../private",
                DriveLetter = 'U'
            }, Catalog, "Storage");

        Assert.False(result.Succeeded);
        Assert.Equal("path.remote.traversal", result.Error?.Code);
    }

    [Theory]
    [InlineData("//srv/harbour", "path.remote.emptySegment")]
    [InlineData("srv//harbour", "path.remote.emptySegment")]
    [InlineData("srv\\harbour", "path.remote.invalid")]
    public void CreateMount_RejectsMalformedRemotePaths(string path, string code)
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "default",
                Username = "user",
                DisplayName = "Storage",
                RemotePath = path,
                DriveLetter = 'U'
            }, Catalog, "Storage");

        Assert.False(result.Succeeded);
        Assert.Equal(code, result.Error?.Code);
    }

    [Fact]
    public void CreateMount_RejectsBlankDriveName()
    {
        var result = ProfileSetupPlan.CreateMount(
            new ProfileSetupRequest
            {
                ProfileId = "default",
                Username = "user",
                DisplayName = "   ",
                DriveLetter = 'U'
            }, Catalog, "Storage");

        Assert.False(result.Succeeded);
        Assert.Equal("mount.displayName.required", result.Error?.Code);
    }

    private static ISetupProfileCatalog Catalog { get; } = new SetupProfileCatalog(
        [
            Profile("default", []),
            Profile("vessel", ["--poll-interval", "0", "--contimeout", "60m", "--retries", "100"])
        ],
        ProfileCatalogSource.AdjacentFile);

    private static SetupProfile Profile(string id, IReadOnlyList<string> arguments) => new()
    {
        Id = id,
        DisplayName = id,
        Description = "Test profile",
        RemoteName = "Storage",
        Connection = new WebDavConnectionDefinition
        {
            BaseUrl = new Uri("https://storage.example.org/"),
            PathTemplate = "/dav/{username}",
            Vendor = WebDavVendor.Nextcloud
        },
        MountArguments = arguments
    };
}
