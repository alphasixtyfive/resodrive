namespace ResoDrive.Windows;

internal interface IRcloneProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? standardErrorLineReceived = null);
}

internal sealed class RcloneProcessRunner : IRcloneProcessRunner
{
    public Task<ProcessRunResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<string>? standardErrorLineReceived = null) =>
        ProcessRunner.RunAsync(
            executablePath,
            arguments,
            timeout,
            standardInput: null,
            environment: null,
            standardErrorLineReceived: standardErrorLineReceived,
            cancellationToken);
}
