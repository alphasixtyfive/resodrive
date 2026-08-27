using System.Net;
using System.Text;
using ResoDrive.Core;
using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class ApplicationUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_ReportsNewerStableRelease()
    {
        using var client = Client(HttpStatusCode.OK,
            """{"tag_name":"v0.3.0","html_url":"https://github.com/alphasixtyfive/resodrive/releases/tag/v0.3.0"}""");
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
    }

    [Fact]
    public async Task CheckAsync_DoesNotTrustNonGitHubReleaseLink()
    {
        using var client = Client(HttpStatusCode.OK,
            """{"tag_name":"v0.2.29","html_url":"https://example.com/download"}""");
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://api.github.test/releases/latest"));

        var result = await service.CheckAsync("0.2.29");

        Assert.True(result.Succeeded);
        Assert.False(result.Value?.UpdateAvailable);
        Assert.Equal(
            ProductLinks.Releases.AbsoluteUri,
            result.Value?.ReleasePage.AbsoluteUri);
    }

    [Fact]
    public async Task CheckAsync_HandlesRepositoryWithoutPublishedRelease()
    {
        using var client = Client(HttpStatusCode.NotFound, "{}");
        var service = new ApplicationUpdateService(
            client,
            new Uri("https://api.github.test/releases/latest"));

        var result = await service.CheckAsync("0.2.29");

        Assert.False(result.Succeeded);
        Assert.Equal("app.update_check_failed", result.Error?.Code);
        Assert.Contains("No published", result.Error?.Message);
    }

    private static HttpClient Client(HttpStatusCode statusCode, string json) =>
        new(new ResponseHandler(statusCode, json));

    private sealed class ResponseHandler(HttpStatusCode statusCode, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
    }
}
