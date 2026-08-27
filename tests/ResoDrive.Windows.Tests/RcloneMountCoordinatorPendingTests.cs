using ResoDrive.Core.Contracts;
using ResoDrive.Core.Domain;
using ResoDrive.Core.Results;
using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class RcloneMountCoordinatorPendingTests
{
    [Fact]
    public async Task MarkPending_PublishesEveryQueuedMountImmediately()
    {
        var paths = TestPaths();
        await using var coordinator = new RcloneMountCoordinator(
            Path.Combine(paths.Root, "rclone.exe"),
            paths.ConfigFile,
            paths,
            new EmptyInventory());
        var first = Definition('R');
        var second = Definition('S');
        Assert.True((await coordinator.ReconcileAsync([first, second])).Succeeded);

        coordinator.MarkPending(first.Id, stopping: false);
        coordinator.MarkPending(second.Id, stopping: false);

        var snapshots = coordinator.GetSnapshots().ToDictionary(snapshot => snapshot.MountId);
        Assert.Equal(MountLifecycle.Starting, snapshots[first.Id].Lifecycle);
        Assert.Equal("Mount queued", snapshots[first.Id].StatusText);
        Assert.Equal(MountLifecycle.Starting, snapshots[second.Id].Lifecycle);
        Assert.Equal("Mount queued", snapshots[second.Id].StatusText);
    }

    [Fact]
    public async Task MarkPending_DoesNotReplaceAnExistingActiveState()
    {
        var paths = TestPaths();
        await using var coordinator = new RcloneMountCoordinator(
            Path.Combine(paths.Root, "rclone.exe"),
            paths.ConfigFile,
            paths,
            new EmptyInventory());
        var mount = Definition('R');
        Assert.True((await coordinator.ReconcileAsync([mount])).Succeeded);
        coordinator.MarkPending(mount.Id, stopping: false);

        // A duplicate queued start must preserve the current in-progress state instead of
        // publishing a second, potentially stale transition.
        coordinator.MarkPending(mount.Id, stopping: false);

        var snapshot = Assert.Single(coordinator.GetSnapshots());
        Assert.Equal(MountLifecycle.Starting, snapshot.Lifecycle);
        Assert.Equal("Mount queued", snapshot.StatusText);
    }

    [Fact]
    public void LaunchChanged_WhenDriveNameChanges()
    {
        var current = Definition('R');
        var renamed = current with { DisplayName = "Renamed drive" };

        Assert.True(RcloneMountCoordinator.LaunchChanged(current, renamed));
    }

    private static MountDefinition Definition(char drive) => new()
    {
        Id = MountId.New(),
        DisplayName = $"Drive {drive}",
        RemoteName = $"remote-{char.ToLowerInvariant(drive)}",
        Target = new MountTarget.Drive(drive),
        AutoMount = AutoMountPolicy.OnApplicationStart
    };

    private static ApplicationPaths TestPaths() => new(
        Path.Combine(Path.GetTempPath(), "rdrive-tests", Guid.NewGuid().ToString("N")));

    private sealed class EmptyInventory : IMountTargetInventory
    {
        public Task<OperationResult<IReadOnlySet<char>>> GetOccupiedDriveLettersAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlySet<char>>(new HashSet<char>()));

        public Task<OperationResult<bool>> IsMountedAsync(
            MountTarget target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(false));
    }
}
