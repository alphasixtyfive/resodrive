using ResoDrive.Core.Results;

namespace ResoDrive.Windows;

public sealed record RcloneConnectionMetadata(string? Host, string? Type);

public static class RcloneConnectionMetadataService
{
    private static readonly TimeSpan InspectionTimeout = TimeSpan.FromSeconds(8);

    public static async Task<OperationResult<IReadOnlyDictionary<string, RcloneConnectionMetadata>>> ReadAsync(
        string rclonePath,
        ApplicationPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rclonePath);
        ArgumentNullException.ThrowIfNull(paths);
        if (!File.Exists(rclonePath) || !File.Exists(paths.ConfigFile))
        {
            return Result.Success<IReadOnlyDictionary<string, RcloneConnectionMetadata>>(
                new Dictionary<string, RcloneConnectionMetadata>(StringComparer.OrdinalIgnoreCase));
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
                return Result.Failure<IReadOnlyDictionary<string, RcloneConnectionMetadata>>(
                    "rclone.metadata_failed",
                    "Storage connection details could not be inspected.",
                    true);
            }

            return Result.Success<IReadOnlyDictionary<string, RcloneConnectionMetadata>>(
                Parse(result.StandardOutput));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or
                System.ComponentModel.Win32Exception)
        {
            return Result.Failure<IReadOnlyDictionary<string, RcloneConnectionMetadata>>(
                "rclone.metadata_failed",
                exception.Message,
                true);
        }
    }

    internal static IReadOnlyDictionary<string, RcloneConnectionMetadata> Parse(string text)
    {
        var connections = new Dictionary<string, RcloneConnectionMetadata>(StringComparer.OrdinalIgnoreCase);
        string? remote = null;
        string? host = null;
        string? type = null;
        void SaveRemote()
        {
            if (!string.IsNullOrWhiteSpace(remote) &&
                (!string.IsNullOrWhiteSpace(host) || !string.IsNullOrWhiteSpace(type)))
            {
                connections[remote] = new RcloneConnectionMetadata(host, type);
            }
        }

        foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length >= 3 && line[0] == '[' && line[^1] == ']')
            {
                SaveRemote();
                remote = line[1..^1].Trim();
                host = null;
                type = null;
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
            if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                type = value.ToLowerInvariant() switch
                {
                    "webdav" => "WebDAV",
                    "sftp" => "SFTP",
                    _ => null,
                };
            }
            else if (key.Equals("url", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                host = uri.IdnHost;
            }
            else if (key.Equals("host", StringComparison.OrdinalIgnoreCase) &&
                     Uri.CheckHostName(value) != UriHostNameType.Unknown)
            {
                host = value;
            }

        }

        SaveRemote();
        return connections;
    }
}
