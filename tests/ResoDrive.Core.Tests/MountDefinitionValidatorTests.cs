using ResoDrive.Core.Domain;
using ResoDrive.Core.Validation;

namespace ResoDrive.Core.Tests;

public sealed class MountDefinitionValidatorTests
{
    private readonly MountDefinitionValidator _validator = new();

    [Fact]
    public void Validate_RejectsInvalidConnectionHost()
    {
        var result = _validator.Validate(
            ValidationTestData.ValidMount('U') with { ConnectionHost = "not a host" });

        Assert.Contains(result.Issues, issue => issue.Code == "mount.connectionHost");
    }

    [Fact]
    public void Validate_AcceptsValidMountAndNestedSyncJob()
    {
        var mount = ValidationTestData.ValidMount(syncJobs: [ValidationTestData.ValidSync()]);

        Assert.True(_validator.Validate(mount).IsValid);
    }

    [Fact]
    public void Validate_AcceptsAbsoluteRemotePath()
    {
        var mount = ValidationTestData.ValidMount() with { RemotePath = "/srv/harbour" };

        Assert.True(_validator.Validate(mount).IsValid);
    }

    [Fact]
    public void Validate_RejectsEmptyIdentityNameAndRemote()
    {
        var mount = ValidationTestData.ValidMount(id: new MountId(Guid.Empty)) with
        {
            DisplayName = "",
            RemoteName = "bad:remote"
        };

        var result = _validator.Validate(mount);

        Assert.Contains(result.Issues, issue => issue.Code == "mount.id");
        Assert.Contains(result.Issues, issue => issue.Code == "mount.displayName.required");
        Assert.Contains(result.Issues, issue => issue.Code == "mount.remoteName.invalid");
    }

    [Fact]
    public void Validate_RejectsSystemDriveAndRootDirectoryTarget()
    {
        var systemDrive = _validator.Validate(ValidationTestData.ValidMount('C'));
        var rootDirectory = _validator.Validate(
            ValidationTestData.ValidMount() with { Target = new MountTarget.Directory(@"C:\") });

        Assert.Contains(systemDrive.Issues, issue => issue.Code == "mount.target.drive");
        Assert.Contains(rootDirectory.Issues, issue => issue.Code == "mount.target.directory.root");
    }

    [Theory]
    [InlineData(-1, 2, 60, "mount.restart.attempts")]
    [InlineData(101, 2, 60, "mount.restart.attempts")]
    [InlineData(5, 0, 60, "mount.restart.initialDelay")]
    [InlineData(5, 60, 30, "mount.restart.maximumDelay")]
    [InlineData(5, 2, 3_601, "mount.restart.maximumDelay")]
    public void Validate_RejectsInvalidRestartPolicy(
        int attempts,
        int initialSeconds,
        int maximumSeconds,
        string code)
    {
        var mount = ValidationTestData.ValidMount() with
        {
            Restart = new RestartPolicy
            {
                MaximumAttempts = attempts,
                InitialDelay = TimeSpan.FromSeconds(initialSeconds),
                MaximumDelay = TimeSpan.FromSeconds(maximumSeconds)
            }
        };

        Assert.Contains(_validator.Validate(mount).Issues, issue => issue.Code == code);
    }

    [Fact]
    public void Validate_RejectsDuplicateNestedSyncIds()
    {
        var id = SyncJobId.New();
        var mount = ValidationTestData.ValidMount(syncJobs:
        [
            ValidationTestData.ValidSync(id),
            ValidationTestData.ValidSync(id) with { DisplayName = "Second" }
        ]);

        Assert.Contains(_validator.Validate(mount).Issues, issue => issue.Code == "mount.syncJobs.duplicateId");
    }

    [Fact]
    public void ValidateCatalog_RejectsDuplicateMountIdsAndDrives()
    {
        var id = MountId.New();
        var mounts = new[]
        {
            ValidationTestData.ValidMount('R', id),
            ValidationTestData.ValidMount('R', id) with { DisplayName = "Second" }
        };

        var result = _validator.ValidateCatalog(mounts);

        Assert.Contains(result.Issues, issue => issue.Code == "catalog.duplicateMountId");
        Assert.Contains(result.Issues, issue => issue.Code == "catalog.duplicateDrive");
    }

    [Fact]
    public void ValidateCatalog_RejectsCanonicalDuplicateDirectories()
    {
        var mounts = new[]
        {
            ValidationTestData.ValidMount() with { Target = new MountTarget.Directory(@"C:\Data\Mount") },
            ValidationTestData.ValidMount() with { Target = new MountTarget.Directory(@"C:\Data\.\Mount") }
        };

        Assert.Contains(
            _validator.ValidateCatalog(mounts).Issues,
            issue => issue.Code == "catalog.duplicateDirectory");
    }

    [Fact]
    public void ValidateCatalog_RejectsDuplicateSyncIdsAcrossMounts()
    {
        var syncId = SyncJobId.New();
        var mounts = new[]
        {
            ValidationTestData.ValidMount('R', syncJobs: [ValidationTestData.ValidSync(syncId)]),
            ValidationTestData.ValidMount('S', syncJobs: [ValidationTestData.ValidSync(syncId)])
        };

        Assert.Contains(
            _validator.ValidateCatalog(mounts).Issues,
            issue => issue.Code == "catalog.duplicateSyncJobId");
    }

    [Fact]
    public void ValidateCatalog_RejectsAmbiguousDisplayNames()
    {
        var duplicateJobName = ValidationTestData.ValidSync();
        var mounts = new[]
        {
            ValidationTestData.ValidMount('R', syncJobs:
            [
                duplicateJobName,
                ValidationTestData.ValidSync() with { DisplayName = duplicateJobName.DisplayName.ToUpperInvariant() }
            ]),
            ValidationTestData.ValidMount('S') with { DisplayName = "DOCUMENTS", RemoteName = "other" }
        };

        var result = _validator.ValidateCatalog(mounts);

        Assert.Contains(result.Issues, issue => issue.Code == "catalog.duplicateMountName");
        Assert.Contains(result.Issues, issue => issue.Code == "catalog.duplicateSyncJobName");
    }

    [Fact]
    public void ValidateCatalog_AcceptsDistinctValidMounts()
    {
        var mounts = new[]
        {
            ValidationTestData.ValidMount('R'),
            ValidationTestData.ValidMount('S') with { DisplayName = "Photos", RemoteName = "photos" }
        };

        Assert.True(_validator.ValidateCatalog(mounts).IsValid);
    }
}
