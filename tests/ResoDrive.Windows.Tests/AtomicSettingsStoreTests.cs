using ResoDrive.Core.Settings;
using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class AtomicSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_FallsBackToSemanticallyValidBackup()
    {
        var paths = TestPaths();
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.SettingsFile, """
            { "schemaVersion": 1, "revision": 3, "application": {}, "mounts": null }
            """);
        await File.WriteAllTextAsync(paths.SettingsFile + ".bak", """
            { "schemaVersion": 1, "revision": 2, "application": {}, "mounts": [] }
            """);
        using var store = new AtomicSettingsStore(paths);

        var result = await store.LoadAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value?.Revision);
    }

    [Fact]
    public async Task SaveAsync_RejectsInvalidSettingsWithoutChangingFile()
    {
        var paths = TestPaths();
        using var store = new AtomicSettingsStore(paths);
        var initial = await store.SaveAsync(new ManagerSettings(), 0);
        Assert.True(initial.Succeeded);
        var before = await File.ReadAllTextAsync(paths.SettingsFile);

        var invalid = initial.Value! with { Mounts = null! };
        var result = await store.SaveAsync(invalid, initial.Value!.Revision);

        Assert.False(result.Succeeded);
        Assert.Equal("settings.invalid", result.Error?.Code);
        Assert.Equal(before, await File.ReadAllTextAsync(paths.SettingsFile));
    }

    private static ApplicationPaths TestPaths() => new(
        Path.Combine(Path.GetTempPath(), "rdrive-tests", Guid.NewGuid().ToString("N")));
}
