using System.Reflection;

namespace ResoDrive.Core;

/// <summary>Build-time product links shared by the UI and update infrastructure.</summary>
public static class ProductLinks
{
    private static readonly Dictionary<string, string> Metadata =
        typeof(ProductLinks).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .GroupBy(attribute => attribute.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value ?? string.Empty,
                StringComparer.Ordinal);

    public static Uri Developer { get; } = ReadUri("DeveloperUrl");
    public static Uri Repository { get; } = ReadUri("RepositoryUrl");
    public static Uri Releases { get; } = ReadUri("RepositoryReleasesUrl");
    public static Uri Issues { get; } = ReadUri("RepositoryIssuesUrl");
    public static Uri LatestReleaseApi { get; } = ReadUri("RepositoryLatestReleaseApiUrl");

    private static Uri ReadUri(string key)
    {
        if (!Metadata.TryGetValue(key, out var value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Build metadata '{key}' must be an absolute HTTPS URL.");
        }

        return uri;
    }
}
