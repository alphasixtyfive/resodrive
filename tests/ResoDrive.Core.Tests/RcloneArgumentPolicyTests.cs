using ResoDrive.Core.Validation;

namespace ResoDrive.Core.Tests;

public sealed class RcloneArgumentPolicyTests
{
    [Fact]
    public void ValidateMount_AcceptsApprovedTokenizedArguments()
    {
        var result = RcloneArgumentPolicy.ValidateMount(
            ["--vfs-cache-mode", "full", "--dir-cache-time=5m", "--read-only", "--network-mode"]);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("--config", "arguments.managerOwned")]
    [InlineData("--config=other.conf", "arguments.managerOwned")]
    [InlineData("--password-command=cmd.exe", "arguments.externalCommand")]
    [InlineData("--rc", "arguments.remoteControl")]
    [InlineData("--rc-no-auth", "arguments.remoteControl")]
    [InlineData("--dump-headers", "arguments.dump")]
    [InlineData("--metadata-mapper=tool.exe", "arguments.externalCommand")]
    [InlineData("--unknown-option", "arguments.unsupported")]
    [InlineData("--", "arguments.terminator")]
    [InlineData("remote:path", "arguments.positional")]
    public void ValidateMount_RejectsUnsafeOrUnapprovedToken(string token, string code)
    {
        var result = RcloneArgumentPolicy.ValidateMount([token]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    [Fact]
    public void ValidateMount_RejectsDuplicateOptionAcrossValueForms()
    {
        var result = RcloneArgumentPolicy.ValidateMount(
            ["--timeout", "1m", "--timeout=2m"]);

        Assert.Contains(result.Issues, issue => issue.Code == "arguments.duplicate");
    }

    [Fact]
    public void ValidateMount_RejectsMissingValue()
    {
        var result = RcloneArgumentPolicy.ValidateMount(["--timeout", "--read-only"]);

        Assert.Contains(result.Issues, issue => issue.Code == "arguments.missingValue");
    }

    [Fact]
    public void ValidateMount_RejectsValueOnSwitch()
    {
        var result = RcloneArgumentPolicy.ValidateMount(["--read-only=true"]);

        Assert.Contains(result.Issues, issue => issue.Code == "arguments.unexpectedValue");
    }

    [Fact]
    public void ValidateSync_AcceptsSyncOptionsAndRejectsMountOnlyOptions()
    {
        Assert.True(RcloneArgumentPolicy.ValidateSync(["--checksum", "--max-age=7d"]).IsValid);
        Assert.Contains(
            RcloneArgumentPolicy.ValidateSync(["--vfs-cache-mode=full"]).Issues,
            issue => issue.Code == "arguments.unsupported");
    }

    [Fact]
    public void ValidateMount_RejectsControlCharactersAndOversizedInput()
    {
        var control = RcloneArgumentPolicy.ValidateMount(["--timeout", "1m\n--rc"]);
        var inlineControl = RcloneArgumentPolicy.ValidateMount(["--timeout=1m\n--rc"]);
        var oversized = RcloneArgumentPolicy.ValidateMount(
            Enumerable.Repeat("--read-only", 65).ToArray());

        Assert.Contains(control.Issues, issue => issue.Code == "arguments.controlCharacter");
        Assert.Contains(inlineControl.Issues, issue => issue.Code == "arguments.controlCharacter");
        Assert.Contains(oversized.Issues, issue => issue.Code == "arguments.tooMany");
    }
}
