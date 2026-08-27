using System.Net;
using System.Security.Cryptography;
using System.Text;
using ResoDrive.Core;
using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class ApplicationUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_ReportsNewerStableRelease()
    {
        using var client = Client(
            HttpStatusCode.OK,
            "https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.0");
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://api.github.test/releases/latest"));

        var result = await service.CheckAsync("0.2.28");

        Assert.True(result.Succeeded);
        Assert.True(result.Value?.UpdateAvailable);
        Assert.Equal("0.3.0", result.Value?.AvailableVersion);
        Assert.Equal(
            "https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.0",
            result.Value?.ReleasePage.AbsoluteUri);
        Assert.EndsWith("resodrive-win-x64-0.3.0.msi", result.Value?.InstallerDownload?.AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_DoesNotTrustNonRepositoryReleaseLink()
    {
        using var client = Client(HttpStatusCode.OK, "https://example.com/releases/tag/v0.2.29");
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://api.github.test/releases/latest"));

        var result = await service.CheckAsync("0.2.29");

        Assert.False(result.Succeeded);
        Assert.Equal("app.update_response_invalid", result.Error?.Code);
    }

    [Fact]
    public async Task CheckAsync_HandlesRepositoryWithoutPublishedRelease()
    {
        using var client = Client(HttpStatusCode.NotFound, ProductLinks.LatestRelease.AbsoluteUri);
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://api.github.test/releases/latest"));

        var result = await service.CheckAsync("0.2.29");

        Assert.False(result.Succeeded);
        Assert.Equal("app.update_check_failed", result.Error?.Code);
        Assert.Contains("No published", result.Error?.Message);
    }

    [Fact]
    public async Task CheckAsync_RejectsLatestLinkThatDidNotRedirect()
    {
        using var client = Client(HttpStatusCode.OK, ProductLinks.LatestRelease.AbsoluteUri);
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://api.github.test/releases/latest"));

        var result = await service.CheckAsync("0.2.29");

        Assert.False(result.Succeeded);
        Assert.Equal("app.update_response_invalid", result.Error?.Code);
    }

    [Fact]
    public async Task CheckAsync_RejectsPrereleaseTag()
    {
        using var client = Client(
            HttpStatusCode.OK,
            "https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.0-beta.1");
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://github.test/releases/latest"));

        var result = await service.CheckAsync("0.2.29");

        Assert.False(result.Succeeded);
        Assert.Equal("app.update_response_invalid", result.Error?.Code);
    }

    [Fact]
    public async Task DownloadInstallerAsync_VerifiesChecksumAndReplacesOldPackage()
    {
        var installerUri = new Uri(
            "https://github.com/alphasixtyfive/resodrive/releases/download/v0.3.0/resodrive-win-x64-0.3.0.msi");
        var checksumUri = new Uri(installerUri.AbsoluteUri + ".sha256");
        var payload = Encoding.UTF8.GetBytes("verified installer payload");
        var checksum = Encoding.ASCII.GetBytes(
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant() + "  resodrive-win-x64-0.3.0.msi");
        using var client = new HttpClient(new RoutingHandler(new Dictionary<string, byte[]>
        {
            [installerUri.AbsoluteUri] = payload,
            [checksumUri.AbsoluteUri] = checksum,
        }));
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://api.github.test/releases/latest"));
        var directory = Path.Combine(Path.GetTempPath(), "resodrive-app-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "resodrive-win-x64-0.2.29.msi"), "old");
        try
        {
            var result = await service.DownloadInstallerAsync(
                new ApplicationUpdateCheck(
                    "0.2.29",
                    "0.3.0",
                    true,
                    new Uri("https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.0"),
                    installerUri,
                    checksumUri),
                directory);

            Assert.True(result.Succeeded, result.Error?.Message);
            Assert.Equal(payload, await File.ReadAllBytesAsync(result.Value!.InstallerPath));
            Assert.False(File.Exists(Path.Combine(directory, "resodrive-win-x64-0.2.29.msi")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static HttpClient Client(HttpStatusCode statusCode, string finalUri) =>
        new(new ResponseHandler(statusCode, new Uri(finalUri)));

    private sealed class ResponseHandler(HttpStatusCode statusCode, Uri finalUri) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = new HttpRequestMessage(request.Method, finalUri),
            });
    }

    private sealed class RoutingHandler(IReadOnlyDictionary<string, byte[]> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var key = request.RequestUri?.AbsoluteUri ?? string.Empty;
            return Task.FromResult(responses.TryGetValue(key, out var content)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(content),
                    RequestMessage = request,
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
    }
}
