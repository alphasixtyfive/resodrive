using ResoDrive.Core.Domain;
using ResoDrive.Core.Settings;

namespace ResoDrive.Windows.Tests;

public sealed class MountDefinitionMapperTests
{
    [Fact]
    public void ToDomain_RejectsIncompleteNestedSettingsWithoutThrowing()
    {
        var missingTarget = ValidSettings() with { Target = null! };
        var missingSchedule = ValidSettings() with
        {
            SyncJobs = [ValidSettings().SyncJobs[0] with { Schedule = null! }]
        };

        Assert.Equal("mount.settings_incomplete", MountDefinitionMapper.ToDomain(missingTarget).Error?.Code);
        Assert.Equal("sync.settings_incomplete", MountDefinitionMapper.ToDomain(missingSchedule).Error?.Code);
    }

    [Fact]
    public void ToDomain_RejectsInvalidDomainValues()
    {
        var result = MountDefinitionMapper.ToDomain(ValidSettings() with { DisplayName = "" });

        Assert.False(result.Succeeded);
        Assert.Equal("mount.displayName.required", result.Error?.Code);
    }

    [Fact]
    public void Mapping_PreservesConnectionHost()
    {
        var mapped = MountDefinitionMapper.ToDomain(
            ValidSettings() with
            {
                ConnectionHost = "cloud.example.com",
                ConnectionType = "WebDAV",
            });

        Assert.True(mapped.Succeeded);
        Assert.Equal("cloud.example.com", mapped.Value?.ConnectionHost);
        Assert.Equal("WebDAV", mapped.Value?.ConnectionType);
        Assert.Equal(
            "cloud.example.com",
            MountDefinitionMapper.ToSettings(mapped.Value!).ConnectionHost);
        Assert.Equal(
            "WebDAV",
            MountDefinitionMapper.ToSettings(mapped.Value!).ConnectionType);
    }

    private static MountSettings ValidSettings() => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = "Storage",
        RemoteName = "storage",
        Target = new MountTargetSettings { DriveLetter = 'R' },
        SyncJobs =
        [
            new SyncJobSettings
            {
                Id = Guid.NewGuid(),
                DisplayName = "Backup",
                LocalPath = @"C:\Data",
                RemotePath = "backup"
            }
        ]
    };
}
