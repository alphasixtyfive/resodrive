using ResoDrive.Core.Contracts;
using ResoDrive.Core.Domain;
using ResoDrive.Core.Results;

namespace ResoDrive.Windows;

public sealed class MountTargetInventory : IMountTargetInventory
{
    public Task<OperationResult<IReadOnlySet<char>>> GetOccupiedDriveLettersAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            IReadOnlySet<char> letters = DriveInfo.GetDrives()
                .Select(drive => char.ToUpperInvariant(drive.Name[0]))
                .ToHashSet();
            return Task.FromResult(Result.Success(letters));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(Result.Failure<IReadOnlySet<char>>(
                "drives.unavailable",
                exception.Message,
                true));
        }
    }

    public Task<OperationResult<bool>> IsMountedAsync(
        MountTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();
        if (target is MountTarget.Directory)
        {
            return Task.FromResult(Result.Failure<bool>("mount.directory_probe_unsupported", "Directory mount readiness cannot yet be verified safely."));
        }
        var exists = target is MountTarget.Drive drive && Directory.Exists($"{drive.Letter}:\\");
        return Task.FromResult(Result.Success(exists));
    }
}
