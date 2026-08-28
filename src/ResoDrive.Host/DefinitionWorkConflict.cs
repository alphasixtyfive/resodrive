using System.Text.Json;
using ResoDrive.Core.Settings;

namespace ResoDrive.Host;

internal static class DefinitionWorkConflict
{
    public static DefinitionWorkAnalysis Analyze(
        IReadOnlyList<MountSettings> current,
        IReadOnlyList<MountSettings> incoming,
        IEnumerable<Guid> activeMountIds,
        IEnumerable<Guid> activeSyncIds,
        IEnumerable<string> operationKeys)
    {
        var currentById = current.ToDictionary(mount => mount.Id);
        var incomingById = incoming.ToDictionary(mount => mount.Id);
        var changedMountIds = currentById.Keys
            .Union(incomingById.Keys)
            .Where(id =>
                !currentById.TryGetValue(id, out var existing) ||
                !incomingById.TryGetValue(id, out var replacement) ||
                !string.Equals(
                    JsonSerializer.Serialize(existing),
                    JsonSerializer.Serialize(replacement),
                    StringComparison.Ordinal))
            .ToHashSet();

        if (changedMountIds.Count == 0)
            return new(new HashSet<Guid>(), new HashSet<Guid>(), false);

        var changedSyncIds = currentById.Values
            .Concat(incomingById.Values)
            .Where(mount => changedMountIds.Contains(mount.Id))
            .SelectMany(mount => mount.SyncJobs)
            .Select(job => job.Id)
            .ToHashSet();
        var launchChangedMountIds = changedMountIds
            .Where(id =>
                !currentById.TryGetValue(id, out var existing) ||
                !incomingById.TryGetValue(id, out var replacement) ||
                LaunchChanged(existing, replacement))
            .ToHashSet();
        var activeChangedMountIds = activeMountIds
            .Where(launchChangedMountIds.Contains)
            .ToHashSet();
        var hasBlockingWork = activeSyncIds.Any(changedSyncIds.Contains) ||
            operationKeys.Any(key =>
                changedMountIds.Any(id => key == $"mount:{id:N}") ||
                changedSyncIds.Any(id => key == $"sync:{id:N}"));

        return new(changedMountIds, activeChangedMountIds, hasBlockingWork);
    }

    private static bool LaunchChanged(MountSettings current, MountSettings replacement) =>
        current.DisplayName != replacement.DisplayName ||
        current.RemoteName != replacement.RemoteName ||
        current.ConnectionHost != replacement.ConnectionHost ||
        current.RemotePath != replacement.RemotePath ||
        current.Target != replacement.Target ||
        current.Enabled != replacement.Enabled ||
        !current.Arguments.SequenceEqual(replacement.Arguments);
}

internal sealed record DefinitionWorkAnalysis(
    IReadOnlySet<Guid> ChangedMountIds,
    IReadOnlySet<Guid> ActiveChangedMountIds,
    bool HasBlockingWork);
