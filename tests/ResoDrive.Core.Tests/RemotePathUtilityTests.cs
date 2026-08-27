using ResoDrive.Core.Domain;

namespace ResoDrive.Core.Tests;

public sealed class RemotePathUtilityTests
{
    [Theory]
    [InlineData("", "", "")]
    [InlineData("root", "", "root")]
    [InlineData("", "child", "child")]
    [InlineData("/root/", "/child/", "/root/child")]
    [InlineData("root", "/child", "root/child")]
    [InlineData("", "/srv/harbour", "/srv/harbour")]
    public void Combine_PreservesRootedBaseAndJoinsWithOneSeparator(
        string root,
        string child,
        string expected) =>
        Assert.Equal(expected, RemotePathUtility.Combine(root, child));

    [Theory]
    [InlineData(" /srv/harbour/ ", "/srv/harbour")]
    [InlineData("documents/", "documents")]
    [InlineData("/", "")]
    public void Normalize_PreservesOneLeadingSlash(string path, string expected) =>
        Assert.Equal(expected, RemotePathUtility.Normalize(path));

    [Theory]
    [InlineData("../private")]
    [InlineData("folder/./child")]
    [InlineData("folder\\child")]
    [InlineData("//srv/harbour")]
    [InlineData("srv//harbour")]
    public void IsWellFormed_RejectsUnsafeOrAmbiguousPaths(string path) =>
        Assert.False(RemotePathUtility.IsWellFormed(path));

    [Theory]
    [InlineData("", "storage:")]
    [InlineData("documents", "storage:documents")]
    [InlineData("/srv/harbour", "storage:/srv/harbour")]
    public void FormatSource_PreservesRootedRemotePath(string path, string expected) =>
        Assert.Equal(expected, RemotePathUtility.FormatSource("storage", path));

    [Fact]
    public void Display_UsesFriendlyDriveAndFolderNotation() =>
        Assert.Equal("Archive · team/reports", RemotePathUtility.Display("Archive", "team", "reports"));

    [Fact]
    public void Display_DoesNotDuplicateAbsolutePathSeparator() =>
        Assert.Equal("Archive · /srv/harbour", RemotePathUtility.Display("Archive", "/srv", "harbour"));
}
