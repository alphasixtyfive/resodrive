using ResoDrive.Core.Validation;

namespace ResoDrive.Core.Tests;

public sealed class RcloneArgumentTextCodecTests
{
    [Fact]
    public void Format_KeepsOptionValuesOnOneLine()
    {
        var result = RcloneArgumentTextCodec.Format(
            ["--poll-interval", "30s", "--read-only", "--timeout=1m"]);

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "--poll-interval=30s",
                "--read-only",
                "--timeout=1m"),
            result);
    }

    [Fact]
    public void Parse_ReturnsOneTokenPerVisibleLine()
    {
        var result = RcloneArgumentTextCodec.Parse(
            " --poll-interval=30s \r\n\r\n --read-only ");

        Assert.Equal(["--poll-interval=30s", "--read-only"], result);
    }
}
