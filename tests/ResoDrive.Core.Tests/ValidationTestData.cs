using ResoDrive.Core.Domain;

namespace ResoDrive.Core.Tests;

internal static class ValidationTestData
{
    internal static MountDefinition ValidMount(
        char drive = 'R',
        MountId? id = null,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyList<SyncJob>? syncJobs = null) => new()
        {
            Id = id ?? MountId.New(),
            DisplayName = "Documents",
            RemoteName = "cloud",
            RemotePath = "documents",
            Target = new MountTarget.Drive(drive),
            Arguments = arguments ?? ["--vfs-cache-mode", "full", "--read-only"],
            SyncJobs = syncJobs ?? []
        };

    internal static SyncJob ValidSync(
        SyncJobId? id = null,
        IReadOnlyList<string>? arguments = null) => new()
        {
            Id = id ?? SyncJobId.New(),
            DisplayName = "Documents backup",
            LocalPath = @"C:\Data\Documents",
            RemotePath = "backups/documents",
            Mode = SyncMode.CopyToRemote,
            Arguments = arguments ?? ["--checksum", "--transfers=4"]
        };
}
