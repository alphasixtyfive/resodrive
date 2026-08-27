using ResoDrive.Core.Domain;
using ResoDrive.Windows;

namespace ResoDrive.App.Tests;

public sealed class SyncStatusPresentationTests
{
    [Fact]
    public void RunningStatus_UsesStructuredCountersWithoutMixingInTheRoute()
    {
        var status = new HostSyncStatus(
            Guid.NewGuid(),
            Guid.NewGuid(),
            nameof(SyncLifecycle.Running),
            "Syncing",
            null,
            BytesTransferred: 524288,
            TotalBytes: 1048576,
            ProgressPercent: 50,
            TransfersCompleted: 3,
            TotalTransfers: 8,
            SpeedBytesPerSecond: 262144,
            EtaSeconds: 12);

        var result = SyncStatusPresentation.Create(
            SyncMode.CopyToRemote,
            enabled: true,
            busy: true,
            SyncLifecycle.Running,
            status,
            recognized: true);

        Assert.Equal("Syncing 50%", result.Primary);
        Assert.Equal("3 of 8 files · 512 KB of 1.00 MB · 256 KB/s · 12s left", result.Secondary);
    }

    [Fact]
    public void CompletedNoChangeStatus_IsConcise()
    {
        var status = new HostSyncStatus(
            Guid.NewGuid(),
            Guid.NewGuid(),
            nameof(SyncLifecycle.Succeeded),
            "No changes",
            DateTimeOffset.Now,
            ChecksCompleted: 60,
            TotalChecks: 60);

        var result = SyncStatusPresentation.Create(
            SyncMode.CopyFromRemote,
            enabled: true,
            busy: false,
            SyncLifecycle.Succeeded,
            status,
            recognized: true);

        Assert.Equal("Up to date", result.Primary);
        Assert.Contains("60 checked", result.Secondary, StringComparison.Ordinal);
        Assert.Contains("Today", result.Secondary, StringComparison.Ordinal);
    }
}
