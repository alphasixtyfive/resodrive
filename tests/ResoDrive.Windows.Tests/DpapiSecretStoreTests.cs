using System.Security.AccessControl;
using System.Security.Principal;

namespace ResoDrive.Windows.Tests;

public sealed class DpapiSecretStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "resodrive-dpapi-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoad_RoundTripsAndRestrictsFileAccess()
    {
        var paths = new ApplicationPaths(_root);
        var store = new DpapiSecretStore(paths);
        var password = DpapiSecretStore.CreateRandomPassword();

        await store.SaveAsync(password);

        Assert.Equal(password, await store.LoadAsync());
        var security = new FileInfo(paths.ConfigSecretFile).GetAccessControl();
        Assert.True(security.AreAccessRulesProtected);
        var rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToArray();
        Assert.DoesNotContain(rules, rule => rule.AccessControlType == AccessControlType.Allow &&
            rule.IdentityReference is SecurityIdentifier sid &&
            sid.IsWellKnown(WellKnownSidType.WorldSid));
    }

    [Fact]
    public async Task Load_RejectsOversizedProtectedFile()
    {
        var paths = new ApplicationPaths(_root);
        paths.EnsureCreated();
        await File.WriteAllBytesAsync(paths.ConfigSecretFile, new byte[16 * 1024 + 1]);
        var store = new DpapiSecretStore(paths);

        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(
            () => store.LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
