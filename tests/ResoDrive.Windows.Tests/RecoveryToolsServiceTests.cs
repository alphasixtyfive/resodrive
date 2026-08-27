using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class RecoveryToolsServiceTests
{
    [Fact]
    public async Task ExportSettingsAsync_CopiesValidSettingsWithoutSecretFiles()
    {
        var paths = TestPaths();
        paths.EnsureCreated();
        const string settings = "{ \"schemaVersion\": 1, \"revision\": 2, \"application\": {}, \"mounts\": [] }";
        await File.WriteAllTextAsync(paths.SettingsFile, settings);
        await File.WriteAllTextAsync(paths.ConfigSecretFile, "encrypted-secret");
        var destination = Path.Combine(
            Path.GetTempPath(),
            "resodrive-recovery-exports",
            Guid.NewGuid().ToString("N"),
            "settings.json");

        var result = await new RecoveryToolsService(paths).ExportSettingsAsync(destination);

        Assert.True(result.Succeeded, result.Error?.Message);
        Assert.Equal(settings, await File.ReadAllTextAsync(destination));
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!));
    }

    [Fact]
    public async Task ExportSettingsAsync_RejectsEveryManagedDataPath()
    {
        var paths = TestPaths();
        paths.EnsureCreated();
        await File.WriteAllTextAsync(paths.SettingsFile, "{ \"schemaVersion\": 1 }");

        var result = await new RecoveryToolsService(paths).ExportSettingsAsync(paths.ProfilesFile);

        Assert.False(result.Succeeded);
        Assert.Equal("settings.export_same_file", result.Error?.Code);
        Assert.False(File.Exists(paths.ProfilesFile));
    }

    [Theory]
    [InlineData("password=hunter2", "hunter2")]
    [InlineData("password=\"two word secret\"", "two word secret")]
    [InlineData("\"token\": \"json secret\"", "json secret")]
    [InlineData("--password flag-secret", "flag-secret")]
    [InlineData("Authorization Bearer-token", "Bearer-token")]
    [InlineData("https://me:secret@example.test", "me:secret")]
    public void Sanitize_RemovesCredentialValues(string source, string forbidden)
    {
        var sanitized = RecoveryToolsService.Sanitize(source);

        Assert.DoesNotContain(forbidden, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<redacted>", sanitized);
    }

    [Theory]
    [InlineData("server=files.private.example", "files.private.example")]
    [InlineData("address=192.168.10.24", "192.168.10.24")]
    [InlineData("server=NAS01", "NAS01")]
    [InlineData("lookup NAS01: no such host", "NAS01")]
    [InlineData("connecting to NAS01 failed", "NAS01")]
    [InlineData("address=2001:db8:85a3::8a2e:370:7334", "2001:db8")]
    [InlineData("path=\\\\server\\private share\\report.txt", "server")]
    [InlineData("path=/home/alexey/private report.txt", "alexey")]
    [InlineData("path=/opt/private/file", "private")]
    [InlineData("path=C:\\Private Folder\\report.txt", "Private Folder")]
    [InlineData("path=C:/Users/Alex/private.txt", "Alex")]
    public void Sanitize_RemovesHostsAndAbsolutePaths(string source, string forbidden)
    {
        var sanitized = RecoveryToolsService.Sanitize(source);

        Assert.DoesNotContain(forbidden, sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.True(sanitized.Contains("<host>", StringComparison.Ordinal) ||
            sanitized.Contains("<path>", StringComparison.Ordinal) ||
            sanitized.Contains("<user>", StringComparison.Ordinal));
    }

    private static ApplicationPaths TestPaths() => new(
        Path.Combine(Path.GetTempPath(), "resodrive-recovery-tests", Guid.NewGuid().ToString("N")));
}
