namespace ResoDrive.Core.Domain;

public static class RemotePathUtility
{
    public const int MaximumLength = 2_048;

    public static string Normalize(string? path)
    {
        var normalized = (path ?? string.Empty).Trim();
        if (normalized == "/")
        {
            return string.Empty;
        }

        return normalized.EndsWith('/') &&
               !normalized.EndsWith("//", StringComparison.Ordinal)
            ? normalized[..^1]
            : normalized;
    }

    public static bool IsWellFormed(string? path)
    {
        if (path is null || path.Length > MaximumLength || path.Contains('\\') ||
            path.Contains("//", StringComparison.Ordinal) || path.Any(IsUnsafeCharacter))
        {
            return false;
        }

        return !path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
    }

    public static string Combine(string? rootPath, string? childPath)
    {
        var root = Normalize(rootPath);
        var child = Normalize(childPath);
        if (root.Length == 0)
        {
            return child;
        }

        if (child.Length == 0)
        {
            return root;
        }

        return $"{root}/{(child[0] == '/' ? child[1..] : child)}";
    }

    public static string FormatSource(string remoteName, string? path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        return $"{remoteName.Trim().TrimEnd(':')}:{Normalize(path)}";
    }

    public static string Display(string name, string? rootPath, string? childPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var path = Combine(rootPath, childPath);
        return path.Length == 0
            ? name
            : $"{name} · {path}";
    }

    private static bool IsUnsafeCharacter(char value) =>
        value == '\0' || value == '\r' || value == '\n' || char.IsControl(value);
}
