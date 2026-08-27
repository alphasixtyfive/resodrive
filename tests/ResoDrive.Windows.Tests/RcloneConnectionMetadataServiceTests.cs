namespace ResoDrive.Windows.Tests;

public sealed class RcloneConnectionMetadataServiceTests
{
    [Fact]
    public void Parse_ReadsWebDavAndSftpWithoutCredentials()
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

        var connections = RcloneConnectionMetadataService.Parse(redacted);

        Assert.Equal(new RcloneConnectionMetadata("cloud.example.com", "WebDAV"), connections["Cloud"]);
        Assert.Equal(new RcloneConnectionMetadata("files.example.net", "SFTP"), connections["Server"]);
        Assert.Equal(2, connections.Count);
    }

    [Fact]
    public void Parse_IgnoresMalformedEndpoints()
    {
        const string redacted = """
            [Broken]
            url = not a URL
            host = not a host
            """;

        Assert.Empty(RcloneConnectionMetadataService.Parse(redacted));
    }
}
