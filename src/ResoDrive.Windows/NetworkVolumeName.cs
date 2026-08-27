using System.Buffers;

namespace ResoDrive.Windows;

internal static class NetworkVolumeName
{
    private const int MaximumShareNameLength = 80;
    private static readonly SearchValues<char> InvalidShareCharacters =
        SearchValues.Create("\"/\\[]:|<>+=;,?*");

    public static string? CreateLocal(string displayName)
    {
        var volumeName = displayName.Trim();
        return volumeName.Length == 0 ? null : volumeName;
    }

    public static string? Create(string? host, string displayName, char driveLetter)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var normalizedHost = host.Trim();
        var hostType = Uri.CheckHostName(normalizedHost);
        if (hostType is not UriHostNameType.Dns and not UriHostNameType.IPv4)
        {
            return null;
        }

        var share = displayName.Trim().TrimEnd('.');
        if (share.Length == 0 || share.Length > MaximumShareNameLength ||
            share.AsSpan().ContainsAny(InvalidShareCharacters) || share.Any(char.IsControl))
        {
            share = $"ResoDrive-{char.ToUpperInvariant(driveLetter)}";
        }

        return $@"\\{normalizedHost}\{share}";
    }
}
