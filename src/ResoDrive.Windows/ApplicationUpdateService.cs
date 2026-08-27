using System.Net.Http.Headers;
using System.Text.Json;
using ResoDrive.Core;
using ResoDrive.Core.Results;

namespace ResoDrive.Windows;

public sealed record ApplicationUpdateCheck(
    string CurrentVersion,
    string AvailableVersion,
    bool UpdateAvailable,
    Uri ReleasePage);

/// <summary>Checks the latest published stable ResoDrive release without installing it.</summary>
public sealed class ApplicationUpdateService
{
    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient _client;
    private readonly Uri _endpoint;

    public ApplicationUpdateService()
        : this(SharedClient, ProductLinks.LatestReleaseApi)
    {
    }

    internal ApplicationUpdateService(HttpClient client, Uri endpoint)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public async Task<OperationResult<ApplicationUpdateCheck>> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (!TryVersion(currentVersion, out var current))
        {
            return Result.Failure<ApplicationUpdateCheck>(
                "app.update_current_invalid",
                "The installed ResoDrive version is not recognized.");
        }

        try
        {
            using var response = await _client.GetAsync(_endpoint, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<ApplicationUpdateCheck>(
                    "app.update_check_failed",
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "No published ResoDrive release is available yet."
                        : "The ResoDrive release service could not be reached.",
                    true);
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()
                : null;
            if (!TryVersion(tag, out var available))
            {
                return Result.Failure<ApplicationUpdateCheck>(
                    "app.update_response_invalid",
                    "The release service returned an invalid version.");
            }

            var releasePage = ProductLinks.Releases;
            if (root.TryGetProperty("html_url", out var pageElement) &&
                Uri.TryCreate(pageElement.GetString(), UriKind.Absolute, out var candidate) &&
                candidate.Scheme == Uri.UriSchemeHttps &&
                candidate.Host.Equals(ProductLinks.Repository.Host, StringComparison.OrdinalIgnoreCase) &&
                candidate.AbsolutePath.StartsWith(
                    ProductLinks.Repository.AbsolutePath.TrimEnd('/') + "/releases/",
                    StringComparison.OrdinalIgnoreCase))
            {
                releasePage = candidate;
            }

            return Result.Success(new ApplicationUpdateCheck(
                currentVersion,
                available.ToString(3),
                available > current,
                releasePage));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<ApplicationUpdateCheck>(
                "app.update_check_timeout",
                "The ResoDrive update check timed out.",
                true);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or JsonException)
        {
            return Result.Failure<ApplicationUpdateCheck>(
                "app.update_check_failed",
                "The ResoDrive update check failed: " + exception.Message,
                true);
        }
    }

    private static bool TryVersion(string? value, out Version version) =>
        Version.TryParse(value?.Trim().TrimStart('v', 'V').Split('+', 2)[0], out version!);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ResoDrive", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
