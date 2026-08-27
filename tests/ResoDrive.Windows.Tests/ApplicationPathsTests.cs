using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class ApplicationPathsTests
{
    [Fact]
    public void Constructor_ResolvesRelativeRootFromApplicationDirectory()
    {
        var paths = new ApplicationPaths(Path.Combine("state", "account"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "state", "account")),
            paths.Root
        );
    }

    [Fact]
    public void Constructor_PreservesAbsoluteRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "rdrive-tests", Guid.NewGuid().ToString("N"));

        var paths = new ApplicationPaths(root);

        Assert.Equal(Path.GetFullPath(root), paths.Root);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "sync-run-state.json"), paths.SyncRunStateFile);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "welcome.complete"), paths.WelcomeCompletedFile);
        Assert.Equal(
            Path.Combine(Path.GetFullPath(root), "components", "rclone", "rclone.exe"),
            paths.RcloneExecutable);
    }
}
