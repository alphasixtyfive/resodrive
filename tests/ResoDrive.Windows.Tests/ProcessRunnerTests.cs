using System.Diagnostics;
using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_TimesOutAndTerminatesProcessTreeWithinBound()
    {
        var stopwatch = Stopwatch.StartNew();

        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            ["/d", "/s", "/c", "ping 127.0.0.1 -n 20 > nul"],
            TimeSpan.FromMilliseconds(100));

        Assert.True(result.TimedOut);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8));
    }
}
