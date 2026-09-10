using System.Globalization;
using System.Text.RegularExpressions;

namespace ResoDrive.Core.Validation;

internal static partial class RcloneOptionValue
{
    public static string? Error(string option, string value)
    {
        switch (option.ToLowerInvariant())
        {
            case "--vfs-cache-mode":
                return value.ToLowerInvariant() is "full" or "writes" or "minimal" or "off"
                    ? null : "Choose full, writes, minimal, or off.";
            case "--vfs-cache-max-size":
            case "--vfs-cache-min-free-space":
            case "--vfs-read-chunk-size-limit":
                if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value == "-1") return null;
                return SizeError(value);
            case "--vfs-read-ahead":
            case "--vfs-read-chunk-size":
            case "--buffer-size":
                if (value.Equals("off", StringComparison.OrdinalIgnoreCase)) return null;
                return SizeError(value);
            case "--vfs-cache-max-age":
            case "--dir-cache-time":
            case "--poll-interval":
            case "--contimeout":
            case "--timeout":
            case "--retries-sleep":
                return DurationError(value);
            case "--vfs-read-chunk-streams":
                return IntegerError(value, 0);
            case "--low-level-retries":
            case "--retries":
            case "--transfers":
            case "--checkers":
                return IntegerError(value, 1);
            default:
                return null;
        }
    }

    private static string? IntegerError(string value, int minimum) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= minimum
            ? null : $"Enter a whole number of at least {minimum}.";

    private static string? SizeError(string value)
    {
        var match = SizePattern().Match(value);
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
        {
            var unit = match.Groups[2].Value.ToUpperInvariant();
            var power = unit.Length == 0 ? 1 : "BKMGTPE".IndexOf(unit[0]);
            if (number * Math.Pow(1024, power) <= long.MaxValue) return null;
        }
        return "Enter a non-negative size such as 512M or 2G within rclone's supported range.";
    }

    private static string? DurationError(string value)
    {
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase)) return null;
        if (!DurationPattern().IsMatch(value))
            return "Enter a non-negative duration such as 30s, 5m, 72h, or 7d.";
        double seconds = 0;
        foreach (Match part in DurationPartPattern().Matches(value))
        {
            if (!double.TryParse(part.Groups[1].Value, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var number)) return "The duration is too large.";
            var multiplier = part.Groups[2].Value switch
            {
                "ns" => 1e-9, "us" or "µs" or "μs" => 1e-6, "ms" => .001,
                "m" => 60, "h" => 3600, "d" => 86400, "w" => 604800,
                "M" => 2592000, "y" => 31536000, _ => 1
            };
            seconds += number * multiplier;
        }
        return double.IsFinite(seconds) && seconds <= long.MaxValue / 1e9 ? null : "The duration is too large.";
    }

    [GeneratedRegex(@"\A(\d+(?:\.\d*)?|\.\d+)(b|[kmgtpe](?:i?b)?)?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizePattern();
    [GeneratedRegex(@"\A(?:(?:\d+(?:\.\d*)?|\.\d+)(?:[dwMy])?|(?:(?:\d+(?:\.\d*)?|\.\d+)(?:ns|us|µs|μs|ms|s|m|h))+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();
    [GeneratedRegex(@"(\d+(?:\.\d*)?|\.\d+)(ns|us|µs|μs|ms|s|m|h|d|w|M|y)?", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPartPattern();
}
