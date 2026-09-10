using ResoDrive.Core.Validation;

namespace ResoDrive.Core.Tests;

public sealed class RcloneMountOptionsTests
{
    [Theory]
    [InlineData("full")]
    [InlineData("writes")]
    [InlineData("minimal")]
    [InlineData("off")]
    [InlineData("FULL")]
    public void EditingPreservesExplicitModeAndCustomCacheValues(string mode)
    {
        string[] original = ["--poll-interval", "0", "--vfs-cache-mode", mode,
            "--vfs-cache-max-size=3.25GiB", "--vfs-cache-max-age", "36h", "--network-mode"];
        var options = new RcloneMountOptions(original);
        Assert.Equal(mode.ToLowerInvariant(), options.CacheMode);
        Assert.Equal(original, options.Compose(options.CacheMode, options.CacheSize, options.CacheAge,
            true, RcloneArgumentTextCodec.Parse(RcloneArgumentTextCodec.Format(options.AdditionalArguments))));
    }

    [Fact]
    public void EditingLegacyMountKeepsOmittedDefaultsAndOnlyChangesSelectedOptions()
    {
        string[] original = ["--poll-interval=0"];
        var options = new RcloneMountOptions(original);
        Assert.True(options.UsesLegacyCacheDefault);
        Assert.Equal("writes", options.CacheMode);
        Assert.Equal("off", options.CacheSize);
        Assert.Equal("1h", options.CacheAge);
        Assert.Equal(original, options.Compose("writes", "off", "1h", false, options.AdditionalArguments));
        Assert.Equal(["--poll-interval=0", "--vfs-cache-mode=full"],
            options.Compose("full", "off", "1h", false, options.AdditionalArguments));
    }

    [Fact]
    public void NewMountGetsExplicitFullAndProfileCacheOptionsStayOutOfAdditionalOptions()
    {
        var manual = new RcloneMountOptions([], newMount: true);
        Assert.Equal(["--vfs-cache-mode=full"], manual.Compose("full", "off", "1h", false, []));
        var profile = new RcloneMountOptions(["--vfs-cache-max-size", "2G", "--vfs-cache-max-age=72h",
            "--poll-interval=0", "--links"], newMount: true);
        Assert.Equal("2G", profile.CacheSize);
        Assert.Equal("72h", profile.CacheAge);
        Assert.Equal(["--poll-interval=0", "--links"], profile.AdditionalArguments);
    }

    [Fact]
    public void ChangingCacheAndAdvancedOptionsEmitsEachControlOnceAndPreservesOtherSettings()
    {
        var options = new RcloneMountOptions(["--vfs-cache-mode", "minimal", "--network-mode",
            "--vfs-cache-max-size=1G", "--poll-interval", "0", "--read-only"]);
        var result = options.Compose("full", "2G", "72h", false, ["--poll-interval=0", "--read-only", "--timeout=15m"]);
        Assert.True(RcloneArgumentPolicy.ValidateMount(result).IsValid);
        Assert.False(RcloneMountOptions.HasOption(result, "--network-mode"));
        Assert.Equal("full", RcloneMountOptions.Value(result, RcloneMountOptions.CacheModeOption));
        Assert.Equal("2G", RcloneMountOptions.Value(result, RcloneMountOptions.CacheSizeOption));
        Assert.Contains("--read-only", result);
        Assert.DoesNotContain("minimal", result);
    }

    [Theory]
    [InlineData("--vfs-cache-mode=nonsense")]
    [InlineData("--vfs-cache-max-size=banana")]
    [InlineData("--vfs-cache-max-size=2bb")]
    [InlineData("--vfs-cache-max-size=999999999999999999E")]
    [InlineData("--vfs-cache-max-age=72hours")]
    [InlineData("--vfs-cache-max-age=1d2h")]
    [InlineData("--timeout=9999999999999h")]
    [InlineData("--vfs-read-chunk-streams=-2")]
    [InlineData("--low-level-retries=0")]
    public void RejectsInvalidTypedValues(string argument) =>
        Assert.Contains(RcloneArgumentPolicy.ValidateMount([argument]).Issues, issue => issue.Code == "arguments.invalidValue");

    [Theory]
    [InlineData("--vfs-cache-mode=FULL")]
    [InlineData("--vfs-cache-max-size=3.25GiB")]
    [InlineData("--vfs-cache-max-size=off")]
    [InlineData("--vfs-cache-max-age=72h")]
    [InlineData("--vfs-cache-max-age=off")]
    [InlineData("--timeout=1h30m")]
    [InlineData("--vfs-read-ahead=32M")]
    [InlineData("--vfs-read-chunk-size-limit=off")]
    [InlineData("--vfs-read-chunk-streams=0")]
    [InlineData("--vfs-cache-min-free-space=1G")]
    public void AcceptsCacheAndAdvancedTuningValues(string argument) =>
        Assert.True(RcloneArgumentPolicy.ValidateMount([argument]).IsValid);
}
