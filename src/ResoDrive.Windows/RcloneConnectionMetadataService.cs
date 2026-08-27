using ResoDrive.Core.Results;

namespace ResoDrive.Windows;

public static class RcloneConnectionMetadataService
{
    private static readonly TimeSpan InspectionTimeout = TimeSpan.FromSeconds(8);

    public static async Task<OperationResult<IReadOnlyDictionary<string, string>>> ReadHostsAsync(
        string rclonePath,
        ApplicationPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rclonePath);
        ArgumentNullException.ThrowIfNull(paths);
        if (!File.Exists(rclonePath) || !File.Exists(paths.ConfigFile))
        {
            return Result.Success<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }

        var arguments = new List<string>
        {
            "config",
            "redacted",
            "--config",
            paths.ConfigFile,
            "--ask-password=false",
        };
        if (File.Exists(paths.ConfigSecretFile))
        {
            arguments.Add("--password-command");
            arguments.Add(RclonePasswordCommand.Create());
        }

        try
        {
            var result = await ProcessRunner.RunAsync(
                rclonePath,
                arguments,
                InspectionTimeout,
                cancellationToken).ConfigureAwait(false);
            if (result.TimedOut || result.ExitCode != 0)
            {
                return Result.Failure<IReadOnlyDictionary<string, string>>(
                    "rclone.metadata_failed",
                    "Storage connection details could not be inspected.",
                    true);
            }

            return Result.Success<IReadOnlyDictionary<string, string>>(
                ParseHosts(result.StandardOutput));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or
                System.ComponentModel.Win32Exception)
        {
            return Result.Failure<IReadOnlyDictionary<string, string>>(
                "rclone.metadata_failed",
                exception.Message,
                true);
        }
    }

    internal static IReadOnlyDictionary<string, string> ParseHosts(string text)
    {
        var hosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? remote = null;
        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length >= 3 && line[0] == '[' && line[^1] == ']')
            {
                remote = line[1..^1].Trim();
                continue;
            }
            if (remote is null)
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            string? host = null;
            if (key.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                host = uri.IdnHost;
            }
            else if (key.Equals("host", StringComparison.OrdinalIgnoreCase) &&
                     Uri.CheckHostName(value) != UriHostNameType.Unknown)
            {
                host = value;
            }

            if (!string.IsNullOrWhiteSpace(host))
            {
                hosts[remote] = host;
            }
        }

        return hosts;
    }
}
