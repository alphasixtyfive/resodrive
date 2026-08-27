namespace ResoDrive.Windows.Tests;

public sealed class HostProtocolTests
{
    [Fact]
    public void AcceptsBaseDirectory_RequiresEquivalentExplicitPath()
    {
        var current = Path.Combine(Path.GetTempPath(), "rdrive-install");

        Assert.False(HostProtocol.AcceptsBaseDirectory(null, current));
        Assert.False(HostProtocol.AcceptsBaseDirectory("  ", current));
        Assert.True(HostProtocol.AcceptsBaseDirectory(current + Path.DirectorySeparatorChar, current));
        Assert.False(HostProtocol.AcceptsBaseDirectory(
            Path.Combine(Path.GetTempPath(), "other-rdrive-install"),
            current));
    }

    [Fact]
    public async Task Request_RoundTripsShutdownConfirmation()
    {
        var request = new HostRequest(
            "shutdown",
            Confirmed: true,
            ExpectedHostBaseDirectory: @"C:\Program Files\rdrive"
        );
        await using var stream = new MemoryStream();

        await HostProtocol.WriteAsync(stream, request, CancellationToken.None);
        stream.Position = 0;
        var restored = await HostProtocol.ReadAsync<HostRequest>(stream, CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal("shutdown", restored.Command);
        Assert.True(restored.Confirmed);
        Assert.Equal(@"C:\Program Files\rdrive", restored.ExpectedHostBaseDirectory);
    }

    [Fact]
    public async Task SendAsync_RejectsNonPositiveTimeout()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            HostClient.SendAsync(new HostRequest("status"), TimeSpan.Zero));
    }

    [Fact]
    public void IsSameBaseDirectory_NormalizesCaseAndTrailingSeparators()
    {
        Assert.True(HostProtocol.IsSameBaseDirectory(
            @"C:\Program Files\rdrive\",
            @"c:\program files\RDRIVE"
        ));
    }
}
