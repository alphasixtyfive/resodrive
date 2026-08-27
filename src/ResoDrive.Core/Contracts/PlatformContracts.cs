using ResoDrive.Core.Domain;
using ResoDrive.Core.Results;

namespace ResoDrive.Core.Contracts;

public sealed record PrerequisiteStatus(bool IsInstalled, string? Version);
public sealed record InstallationStatus(string? Version, string? ExecutablePath);

public interface IMountTargetInventory
{
    Task<OperationResult<IReadOnlySet<char>>> GetOccupiedDriveLettersAsync(
        CancellationToken cancellationToken = default);

    Task<OperationResult<bool>> IsMountedAsync(
        MountTarget target,
        CancellationToken cancellationToken = default);
}
