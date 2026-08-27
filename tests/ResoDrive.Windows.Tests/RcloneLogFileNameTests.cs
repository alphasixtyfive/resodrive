using ResoDrive.Core.Domain;

namespace ResoDrive.Windows.Tests;

public sealed class RcloneLogFileNameTests
{
    [Fact]
    public void ForMount_UsesReadableDriveName()
    {
        var definition = CreateDefinition("Storage", new MountTarget.Drive('U'));

        Assert.Equal("mount-Storage-U.log", RcloneLogFileName.ForMount(definition));
    }

    [Fact]
    public void ForSync_IsReadableSafeAndUnique()
    {
        var definition = CreateDefinition("My / storage", new MountTarget.Drive('U'));
        var job = new SyncJob
        {
            Id = new SyncJobId(Guid.Parse("12345678-0000-0000-0000-000000000000")),
            DisplayName = "Photos: archive",
            LocalPath = @"C:\Photos",
            Mode = SyncMode.CopyToRemote
        };

        Assert.Equal(
            "sync-My-storage-Photos-archive-12345678.jsonl",
            RcloneLogFileName.ForSync(definition, job));
    }

    private static MountDefinition CreateDefinition(string name, MountTarget target) => new()
    {
        Id = MountId.New(),
        DisplayName = name,
        RemoteName = "remote",
        Target = target
    };
}
