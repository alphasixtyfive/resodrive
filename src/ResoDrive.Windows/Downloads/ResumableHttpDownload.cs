using System.Net;
using System.Net.Http.Headers;

namespace ResoDrive.Windows;

internal sealed record DownloadProgress(long BytesReceived, long? TotalBytes);

internal sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
{
    private readonly Action<T> _callback = callback ?? throw new ArgumentNullException(nameof(callback));

    public void Report(T value) => _callback(value);
}

internal static class ResumableHttpDownload
{
    private const int BufferSize = 128 * 1024;

    internal static async Task<DownloadProgress> DownloadAsync(
        HttpClient client,
        Uri source,
        string destination,
        long maximumBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);

        var path = Path.GetFullPath(destination);
        for (var requestAttempt = 0; requestAttempt < 2; requestAttempt++)
        {
            var existingLength = ExistingLength(path, maximumBytes);
            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            if (existingLength > 0)
                request.Headers.Range = new RangeHeaderValue(existingLength, null);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable &&
                response.Content.Headers.ContentRange?.Length == existingLength)
            {
                progress?.Report(new DownloadProgress(existingLength, existingLength));
                return new DownloadProgress(existingLength, existingLength);
            }

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable &&
                requestAttempt == 0)
            {
                DeleteIfPresent(path);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var append = response.StatusCode == HttpStatusCode.PartialContent;
            if (append && response.Content.Headers.ContentRange?.From != existingLength)
                throw new InvalidDataException("The download server returned an invalid byte range.");
            if (!append)
                existingLength = 0;

            var remainingLength = response.Content.Headers.ContentLength;
            var totalLength = response.Content.Headers.ContentRange?.Length ??
                (remainingLength is >= 0 ? checked(existingLength + remainingLength.Value) : null);
            if (totalLength is > 0 && totalLength > maximumBytes)
                throw new InvalidDataException("The download is larger than expected.");

            progress?.Report(new DownloadProgress(existingLength, totalLength));
            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var destinationStream = new FileStream(
                path,
                append ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[BufferSize];
            var received = existingLength;
            while (true)
            {
                var count = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                    break;
                received = checked(received + count);
                if (received > maximumBytes)
                    throw new InvalidDataException("The download is larger than expected.");
                await destinationStream.WriteAsync(buffer.AsMemory(0, count), cancellationToken)
                    .ConfigureAwait(false);
                progress?.Report(new DownloadProgress(received, totalLength));
            }
            await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (totalLength is not null && received != totalLength.Value)
                throw new InvalidDataException("The download ended before all bytes were received.");
            return new DownloadProgress(received, totalLength);
        }

        throw new HttpRequestException("The download could not be restarted.");
    }

    private static long ExistingLength(string path, long maximumBytes)
    {
        if (!File.Exists(path))
            return 0;
        var info = new FileInfo(path);
        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
            info.Length <= 0 || info.Length > maximumBytes)
        {
            DeleteIfPresent(path);
            return 0;
        }
        return info.Length;
    }

    internal static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
