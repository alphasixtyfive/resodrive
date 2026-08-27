namespace ResoDrive.Windows.Tests;

public sealed class RcloneConnectionMetadataServiceTests
{
    [Fact]
    public void ParseHosts_ReadsWebDavAndSftpWithoutCredentials()
    {
        const string redacted = """
            [Cloud]
            type = webdav
            url = https://cloud.example.com/remote.php/dav/files/user
            user = XXX
            pass = XXX
            [Server]
            type = sftp
            host = files.example.net
            user = XXX
            pass = XXX
            """;

        var hosts = RcloneConnectionMetadataService.ParseHosts(redacted);

        Assert.Equal("cloud.example.com", hosts["Cloud"]);
        Assert.Equal("files.example.net", hosts["Server"]);
        Assert.Equal(2, hosts.Count);
    }

    [Fact]
    public void ParseHosts_IgnoresMalformedEndpoints()
    {
        const string redacted = """
            [Broken]
            url = not a URL
            host = not a host
            """;

        Assert.Empty(RcloneConnectionMetadataService.ParseHosts(redacted));
    }
}
