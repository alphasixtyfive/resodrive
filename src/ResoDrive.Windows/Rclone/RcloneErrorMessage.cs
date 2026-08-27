using System.Text.RegularExpressions;

namespace ResoDrive.Windows;

/// <summary>Normalizes rclone diagnostics before they cross into user-facing UI or logs.</summary>
public static partial class RcloneErrorMessage
{
    private const int MaximumLength = 600;

    public static string Clean(string? value, string fallback = "rclone reported an error.")
    {
        var line = value?
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(line))
            return fallback;

        line = LogPrefix().Replace(line, string.Empty);
        line = UrlCredentials().Replace(line, "$1***:***@");
        line = SensitiveQueryValue().Replace(line, "$1***");
        line = SensitiveAssignment().Replace(line, "$1***");
        line = line.Trim();
        if (line.Length == 0)
            return fallback;
        return line.Length <= MaximumLength ? line : line[..MaximumLength] + "…";
    }

    [GeneratedRegex(
        @"^\d{4}[/-]\d{2}[/-]\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?\s+(?:DEBUG|INFO|NOTICE|WARNING|ERROR|CRITICAL):\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LogPrefix();

    [GeneratedRegex(
        @"\b(https?://)([^/\s:@]+):([^@\s/]+)@",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlCredentials();

    [GeneratedRegex(
        @"([?&](?:password|pass|token|secret|api[_-]?key)=)[^&#\s]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveQueryValue();

    [GeneratedRegex(
        @"\b((?:password|pass|token|secret|api[_-]?key)\s*[:=]\s*)[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignment();
}
