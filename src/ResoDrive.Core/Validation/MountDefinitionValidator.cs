using ResoDrive.Core.Domain;

namespace ResoDrive.Core.Validation;

public sealed class MountDefinitionValidator : IValidator<MountDefinition>
{
    private static readonly TimeSpan MinimumRestartDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumRestartDelay = TimeSpan.FromHours(1);
    private const int MaximumRestartAttempts = 100;

    private readonly SyncJobValidator _syncJobValidator = new();

    public ValidationResult Validate(MountDefinition value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var issues = new List<ValidationIssue>();

        ValidationRules.ValidateId(value.Id.Value, "mount.id", "id", issues);
        ValidationRules.ValidateDisplayName(value.DisplayName, "mount", issues);
        ValidationRules.ValidateRemoteName(value.RemoteName, issues);
        ValidateConnectionHost(value.ConnectionHost, issues);
        ValidationRules.ValidateRemotePath(value.RemotePath, "remotePath", issues);
        ValidateTarget(value.Target, issues);
        ValidateRestart(value.Restart, issues);

        if (!Enum.IsDefined(value.AutoMount))
        {
            issues.Add(new("mount.autoMount", "The auto-mount policy is invalid.", "autoMount"));
        }

        foreach (var issue in RcloneArgumentPolicy.ValidateMount(value.Arguments).Issues)
        {
            issues.Add(issue);
        }

        if (value.SyncJobs is null)
        {
            issues.Add(new("mount.syncJobs.null", "The sync job collection cannot be null.", "syncJobs"));
        }
        else
        {
            var syncIds = new HashSet<Guid>();
            for (var index = 0; index < value.SyncJobs.Count; index++)
            {
                var job = value.SyncJobs[index];
                if (job is null)
                {
                    issues.Add(new("mount.syncJobs.nullItem", "Sync jobs cannot be null.", $"syncJobs[{index}]"));
                    continue;
                }

                if (!syncIds.Add(job.Id.Value))
                {
                    issues.Add(new("mount.syncJobs.duplicateId", "Sync job IDs must be unique within a mount.", $"syncJobs[{index}].id"));
                }

                AddNestedIssues(_syncJobValidator.Validate(job), $"syncJobs[{index}]", issues);
            }
        }

        return issues.Count == 0 ? ValidationResult.Valid : new ValidationResult(issues);
    }

    public ValidationResult ValidateCatalog(IEnumerable<MountDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var mounts = definitions.ToArray();
        var issues = new List<ValidationIssue>();
        var ids = new HashSet<Guid>();
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var drives = new Dictionary<char, int>();
        var directories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var syncIds = new HashSet<Guid>();

        for (var index = 0; index < mounts.Length; index++)
        {
            var mount = mounts[index];
            if (mount is null)
            {
                issues.Add(new("catalog.nullMount", "Mount definitions cannot be null.", $"mounts[{index}]"));
                continue;
            }

            AddNestedIssues(Validate(mount), $"mounts[{index}]", issues);
            if (!ids.Add(mount.Id.Value))
            {
                issues.Add(new("catalog.duplicateMountId", "Mount IDs must be unique.", $"mounts[{index}].id"));
            }
            if (!string.IsNullOrWhiteSpace(mount.DisplayName))
            {
                if (names.TryGetValue(mount.DisplayName, out var firstNameIndex))
                {
                    issues.Add(new(
                        "catalog.duplicateMountName",
                        $"The drive name is already used by mount {firstNameIndex + 1}.",
                        $"mounts[{index}].displayName"));
                }
                else
                {
                    names.Add(mount.DisplayName, index);
                }
            }

            switch (mount.Target)
            {
                case MountTarget.Drive drive:
                    if (drives.TryGetValue(drive.Letter, out var firstDriveIndex))
                    {
                        issues.Add(new(
                            "catalog.duplicateDrive",
                            $"Drive {drive.Letter}: is already assigned to mount {firstDriveIndex + 1}.",
                            $"mounts[{index}].target"));
                    }
                    else
                    {
                        drives.Add(drive.Letter, index);
                    }
                    break;
                case MountTarget.Directory directory when TryCanonicalize(directory.Path, out var canonical):
                    if (directories.TryGetValue(canonical, out var firstDirectoryIndex))
                    {
                        issues.Add(new(
                            "catalog.duplicateDirectory",
                            $"The mount directory is already assigned to mount {firstDirectoryIndex + 1}.",
                            $"mounts[{index}].target"));
                    }
                    else
                    {
                        directories.Add(canonical, index);
                    }
                    break;
            }

            if (mount.SyncJobs is null)
            {
                continue;
            }

            var jobNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var syncIndex = 0; syncIndex < mount.SyncJobs.Count; syncIndex++)
            {
                var job = mount.SyncJobs[syncIndex];
                if (job is not null && !string.IsNullOrWhiteSpace(job.DisplayName))
                {
                    if (jobNames.TryGetValue(job.DisplayName, out var firstJobIndex))
                    {
                        issues.Add(new(
                            "catalog.duplicateSyncJobName",
                            $"The sync job name is already used by job {firstJobIndex + 1} on this drive.",
                            $"mounts[{index}].syncJobs[{syncIndex}].displayName"));
                    }
                    else
                    {
                        jobNames.Add(job.DisplayName, syncIndex);
                    }
                }
                if (job is not null && !syncIds.Add(job.Id.Value))
                {
                    issues.Add(new(
                        "catalog.duplicateSyncJobId",
                        "Sync job IDs must be unique across the manager catalog.",
                        $"mounts[{index}].syncJobs[{syncIndex}].id"));
                }
            }
        }

        return issues.Count == 0 ? ValidationResult.Valid : new ValidationResult(issues);
    }

    private static void ValidateTarget(MountTarget? target, List<ValidationIssue> issues)
    {
        switch (target)
        {
            case null:
                issues.Add(new("mount.target.null", "A mount target is required.", "target"));
                break;
            case MountTarget.Drive drive when drive.Letter is < 'D' or > 'Z':
                issues.Add(new("mount.target.drive", "Drive letters must be between D and Z.", "target.letter"));
                break;
            case MountTarget.Directory directory:
                ValidationRules.ValidateDirectoryMountPath(directory.Path, "target.path", issues);
                break;
        }
    }

    private static void ValidateRestart(RestartPolicy? restart, List<ValidationIssue> issues)
    {
        if (restart is null)
        {
            issues.Add(new("mount.restart.null", "A restart policy is required.", "restart"));
            return;
        }

        if (restart.MaximumAttempts is < 0 or > MaximumRestartAttempts)
        {
            issues.Add(new(
                "mount.restart.attempts",
                $"Restart attempts must be between 0 and {MaximumRestartAttempts}.",
                "restart.maximumAttempts"));
        }

        if (restart.InitialDelay < MinimumRestartDelay || restart.InitialDelay > MaximumRestartDelay)
        {
            issues.Add(new(
                "mount.restart.initialDelay",
                "The initial restart delay must be between 1 second and 1 hour.",
                "restart.initialDelay"));
        }

        if (restart.MaximumDelay < restart.InitialDelay || restart.MaximumDelay > MaximumRestartDelay)
        {
            issues.Add(new(
                "mount.restart.maximumDelay",
                "The maximum restart delay must be at least the initial delay and no more than 1 hour.",
                "restart.maximumDelay"));
        }
    }

    private static void ValidateConnectionHost(string? host, List<ValidationIssue> issues)
    {
        if (host is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 ||
            !host.Equals(host.Trim(), StringComparison.Ordinal) ||
            host.Any(char.IsControl) || Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            issues.Add(new(
                "mount.connectionHost",
                "The stored connection host is not a valid DNS name or IP address.",
                "connectionHost"));
        }
    }

    private static void AddNestedIssues(
        ValidationResult result,
        string prefix,
        List<ValidationIssue> issues)
    {
        foreach (var issue in result.Issues)
        {
            issues.Add(issue with { Field = issue.Field is null ? prefix : $"{prefix}.{issue.Field}" });
        }
    }

    private static bool TryCanonicalize(string path, out string canonical)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            canonical = string.Empty;
            return false;
        }

        try
        {
            canonical = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            canonical = string.Empty;
            return false;
        }
    }
}
