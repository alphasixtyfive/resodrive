using System.Diagnostics;
using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class ProcessTerminationTests
{
    [Fact]
    public async Task DisposedExitedProcess_IsTreatedAsExited()
    {
        var process = StartCommand("exit 0");
        await process.WaitForExitAsync();
        process.Dispose();

        Assert.True(ProcessTermination.HasExitedOrUnavailable(process));
        Assert.True(await ProcessTermination.WaitForExitAsync(
            process,
            TimeSpan.FromSeconds(1),
            CancellationToken.None));
    }

    [Fact]
    public async Task RunningProcess_ReturnsAfterDeadline()
    {
        using var process = StartCommand("ping 127.0.0.1 -n 6 > nul");
        try
        {
            var stopwatch = Stopwatch.StartNew();

            var exited = await ProcessTermination.WaitForExitAsync(
                process,
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None);

            Assert.False(exited);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static Process StartCommand(string command) =>
        Process.Start(new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("The test process could not be started.");
}
