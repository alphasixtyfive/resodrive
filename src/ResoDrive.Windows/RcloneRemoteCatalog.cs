using ResoDrive.Core.Contracts;
using ResoDrive.Core.Results;

namespace ResoDrive.Windows;

public sealed class RcloneRemoteCatalog
{
    private readonly Func<(string Executable, string Config)> _pathProvider;
    private readonly ApplicationPaths _applicationPaths;

    public RcloneRemoteCatalog(
        Func<(string Executable, string Config)> pathProvider,
        ApplicationPaths? applicationPaths = null)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _applicationPaths = applicationPaths ?? new ApplicationPaths();
    }

    public async Task<OperationResult<IReadOnlyList<RcloneRemote>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var (executable, config) = _pathProvider();
        if (!File.Exists(executable))
        {
            return Result.Failure<IReadOnlyList<RcloneRemote>>(
                "rclone.not_found",
                $"rclone.exe was not found at '{executable}'.");
        }

        var arguments = new List<string>
        {
            "listremotes", "--config", config, "--ask-password=false"
        };
        if (File.Exists(_applicationPaths.ConfigSecretFile))
        {
            arguments.Add("--password-command");
            arguments.Add(RclonePasswordCommand.Create());
        }

        ProcessRunResult result;
        try
        {
            result = await ProcessRunner.RunAsync(
                executable,
                arguments,
                TimeSpan.FromSeconds(15),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or
                  InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Result.Failure<IReadOnlyList<RcloneRemote>>(
                "rclone.start_failed",
                exception.Message
            );
        }
        if (result.TimedOut)
        {
            return Result.Failure<IReadOnlyList<RcloneRemote>>(
                "rclone.timeout",
                "rclone did not return the remote list in time.",
                true);
        }

        if (result.ExitCode != 0)
        {
            return Result.Failure<IReadOnlyList<RcloneRemote>>(
                "rclone.list_failed",
                SafeError(result.StandardError),
                true);
        }

        var remotes = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseRemote)
            .Where(remote => remote is not null)
            .Cast<RcloneRemote>()
            .OrderBy(remote => remote.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Result.Success<IReadOnlyList<RcloneRemote>>(remotes);
    }

    private static RcloneRemote? ParseRemote(string line)
    {
        var separator = line.IndexOf(':');
        if (separator <= 0)
        {
            return null;
        }

        var name = line[..separator].Trim();
        return name.Length == 0 ? null : new RcloneRemote(name);
    }

    private static string SafeError(string error) =>
        RcloneErrorMessage.Clean(error, "rclone could not read the configured remotes.");
}
