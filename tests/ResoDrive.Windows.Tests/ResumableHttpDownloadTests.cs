using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ResoDrive.Windows.Tests;

public sealed class ResumableHttpDownloadTests
{
    [Fact]
    public async Task DownloadAsync_ContinuesAnExistingPartialFile()
    {
        var content = Encoding.UTF8.GetBytes("a complete download");
        var partialLength = 5;
        using var handler = new RangeHandler(content, partialLength);
        using var client = new HttpClient(handler);
        var directory = Path.Combine(Path.GetTempPath(), "resodrive-download-" + Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "package.download");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(destination, content[..partialLength]);

        try
        {
            var result = await ResumableHttpDownload.DownloadAsync(
                client,
                new Uri("https://downloads.example.test/package"),
                destination,
                1024,
                null,
                CancellationToken.None);

            Assert.Equal(content.Length, result.BytesReceived);
            Assert.Equal(content.Length, result.TotalBytes);
            Assert.Equal(content, await File.ReadAllBytesAsync(destination));
            Assert.Equal(partialLength, handler.RequestedOffset);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RangeHandler(byte[] content, int expectedOffset) : HttpMessageHandler
    {
        public long? RequestedOffset { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedOffset = request.Headers.Range?.Ranges.Single().From;
            Assert.Equal(expectedOffset, RequestedOffset);
            var remaining = content[expectedOffset..];
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent(remaining),
                RequestMessage = request,
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                expectedOffset,
                content.Length - 1,
                content.Length);
            return Task.FromResult(response);
        }
    }
}
