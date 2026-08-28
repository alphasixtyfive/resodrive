using ResoDrive.Core.Settings;
using ResoDrive.Host;

namespace ResoDrive.App.Tests;

public sealed class DefinitionWorkConflictTests
{
    [Fact]
    public void ChangedUnmountedDriveDoesNotConflictWithUnrelatedMountedDrive()
    {
        var edited = Mount('U', "Unimor");
        var mounted = Mount('M', "Mounted");

        var analysis = DefinitionWorkConflict.Analyze(
            [edited, mounted],
            [edited with { DisplayName = "Updated" }, mounted],
            [mounted.Id],
            [],
            []);

        Assert.False(analysis.HasBlockingWork);
        Assert.Empty(analysis.ActiveChangedMountIds);
    }

    [Fact]
    public void ChangedMountedDriveConflicts()
    {
        var edited = Mount('U', "Unimor");

        var analysis = DefinitionWorkConflict.Analyze(
            [edited],
            [edited with { DisplayName = "Updated" }],
            [edited.Id],
            [],
            []);

        Assert.False(analysis.HasBlockingWork);
        Assert.Contains(edited.Id, analysis.ActiveChangedMountIds);
    }

    [Fact]
    public void ChangedDriveConflictsWithItsQueuedSync()
    {
        var syncId = Guid.NewGuid();
        var edited = Mount('U', "Unimor") with
        {
            SyncJobs = [new SyncJobSettings
            {
                Id = syncId,
                DisplayName = "Backup",
                LocalPath = @"C:\Data"
            }]
        };

        var analysis = DefinitionWorkConflict.Analyze(
            [edited],
            [edited with { DisplayName = "Updated" }],
            [],
            [],
            [$"sync:{syncId:N}"]);

        Assert.True(analysis.HasBlockingWork);
    }

    [Fact]
    public void SyncOnlyChangeDoesNotRestartMountedDrive()
    {
        var edited = Mount('U', "Unimor");
        var replacement = edited with
        {
            SyncJobs = [new SyncJobSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = "Backup",
                LocalPath = @"C:\Data"
            }]
        };

        var analysis = DefinitionWorkConflict.Analyze(
            [edited],
            [replacement],
            [edited.Id],
            [],
            []);

        Assert.Contains(edited.Id, analysis.ChangedMountIds);
        Assert.Empty(analysis.ActiveChangedMountIds);
        Assert.False(analysis.HasBlockingWork);
    }

    [Fact]
    public void NewDriveDoesNotInterruptExistingMountedDrive()
    {
        var mounted = Mount('M', "Mounted");
        var added = Mount('U', "Unimor");

        var analysis = DefinitionWorkConflict.Analyze(
            [mounted],
            [mounted, added],
            [mounted.Id],
            [],
            []);

        Assert.Empty(analysis.ActiveChangedMountIds);
        Assert.False(analysis.HasBlockingWork);
    }

    [Fact]
    public void UnrelatedQueuedOperationDoesNotBlockChange()
    {
        var edited = Mount('U', "Unimor");
        var other = Mount('M', "Other");

        var analysis = DefinitionWorkConflict.Analyze(
            [edited, other],
            [edited with { DisplayName = "Updated" }, other],
            [],
            [],
            [$"mount:{other.Id:N}"]);

        Assert.False(analysis.HasBlockingWork);
    }

    private static MountSettings Mount(char driveLetter, string name) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = name,
        RemoteName = "remote",
        Target = new MountTargetSettings { DriveLetter = driveLetter }
    };
}
