using System.Diagnostics;

namespace ResoDrive.Windows;

internal static class ProcessTermination
{
    public static void TryKillTree(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception or
                NotSupportedException)
        {
        }
    }

    public static bool HasExitedOrUnavailable(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        try
        {
            return process.HasExited;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // The exit observer owns disposal. A disposed process has therefore already exited.
            return true;
        }
    }

    public static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (HasExitedOrUnavailable(process))
        {
            return true;
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (
            timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return true;
        }
    }

    private static bool IsUnavailable(Exception exception) =>
        exception is InvalidOperationException or ObjectDisposedException;
}
