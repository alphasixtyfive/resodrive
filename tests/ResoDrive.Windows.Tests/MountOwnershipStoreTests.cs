using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class MountOwnershipStoreTests
{
    [Fact]
    public void IsSameExecutablePath_NormalizesEquivalentPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rdrive-ownership");
        var canonical = Path.Combine(directory, "rclone.exe");
        var equivalent = Path.Combine(directory, ".", "rclone.exe");

        Assert.True(MountOwnershipStore.IsSameExecutablePath(canonical, equivalent));
        Assert.False(MountOwnershipStore.IsSameExecutablePath(
            canonical,
            Path.Combine(Path.GetTempPath(), "portable", "rclone.exe")));
    }

    [Fact]
    public void IsSameExecutablePath_RejectsInvalidPersistedPath()
    {
        Assert.False(MountOwnershipStore.IsSameExecutablePath("rclone.exe", "bad\0path"));
        Assert.False(MountOwnershipStore.IsSameExecutablePath(null, "rclone.exe"));
    }

    [Fact]
    public async Task LoadAsync_RecoversFromAtomicBackupWhenPrimaryIsCorrupt()
    {
        var root = Path.Combine(Path.GetTempPath(), "rdrive-ownership-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new ApplicationPaths(root);
            paths.EnsureCreated();
            using var store = new MountOwnershipStore(paths);
            var first = new OwnedMount(Guid.NewGuid(), 1, DateTime.UtcNow, "rclone.exe", "first:", "R:");
            var second = new OwnedMount(Guid.NewGuid(), 2, DateTime.UtcNow, "rclone.exe", "second:", "S:");
            await store.UpsertAsync(first, CancellationToken.None);
            await store.UpsertAsync(second, CancellationToken.None);
            var backupText = await File.ReadAllTextAsync(paths.OwnershipFile + ".bak");
            Assert.Contains(first.MountId.ToString(), backupText);
            Assert.Single(System.Text.Json.JsonSerializer.Deserialize<OwnedMount[]>(backupText)!);
            await File.WriteAllTextAsync(paths.OwnershipFile, "not json");

            var recovered = await store.LoadAsync(CancellationToken.None);

            Assert.Equal(first.MountId, Assert.Single(recovered).MountId);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
