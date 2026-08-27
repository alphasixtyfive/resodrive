using ResoDrive.Core.Domain;

namespace ResoDrive.Core.Validation;

internal static class ValidationRules
{
    private const int MaximumNameLength = 128;
    private const int MaximumPathLength = RemotePathUtility.MaximumLength;

    internal static void ValidateId(
        Guid id,
        string code,
        string field,
        List<ValidationIssue> issues)
    {
        if (id == Guid.Empty)
        {
            issues.Add(new(code, "The identifier cannot be empty.", field));
        }
    }

    internal static void ValidateDisplayName(
        string? value,
        string codePrefix,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new($"{codePrefix}.displayName.required", "A display name is required.", "displayName"));
            return;
        }

        if (value.Length > MaximumNameLength)
        {
            issues.Add(new($"{codePrefix}.displayName.tooLong", $"Display names cannot exceed {MaximumNameLength} characters.", "displayName"));
        }

        if (value.Any(IsUnsafeCharacter))
        {
            issues.Add(new($"{codePrefix}.displayName.controlCharacter", "Display names cannot contain control characters.", "displayName"));
        }

        if (!value.Equals(value.Trim(), StringComparison.Ordinal))
        {
            issues.Add(new($"{codePrefix}.displayName.whitespace", "Display names cannot start or end with whitespace.", "displayName"));
        }
    }

    internal static void ValidateRemoteName(string? value, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new("mount.remoteName.required", "A remote name is required.", "remoteName"));
            return;
        }

        if (value.Length > MaximumNameLength)
        {
            issues.Add(new("mount.remoteName.tooLong", $"Remote names cannot exceed {MaximumNameLength} characters.", "remoteName"));
        }

        if (!value.Equals(value.Trim(), StringComparison.Ordinal) ||
            value.IndexOfAny([':', '[', ']', '\r', '\n', '\0']) >= 0 ||
            value.Any(char.IsControl))
        {
            issues.Add(new("mount.remoteName.invalid", "The remote name contains reserved or control characters.", "remoteName"));
        }
    }

    internal static void ValidateRemotePath(
        string? value,
        string field,
        List<ValidationIssue> issues)
    {
        if (value is null)
        {
            issues.Add(new("path.remote.null", "Remote paths cannot be null.", field));
            return;
        }

        if (value.Length > MaximumPathLength)
        {
            issues.Add(new("path.remote.tooLong", $"Remote paths cannot exceed {MaximumPathLength} characters.", field));
        }

        if (value.Any(IsUnsafeCharacter) || value.Contains('\\'))
        {
            issues.Add(new("path.remote.invalid", "Remote paths must use forward slashes and cannot contain control characters.", field));
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
        {
            issues.Add(new("path.remote.traversal", "Remote paths cannot contain dot traversal segments.", field));
        }

        if (value.Length > 1 && value.Contains("//", StringComparison.Ordinal))
        {
            issues.Add(new("path.remote.emptySegment", "Remote paths cannot contain empty path segments.", field));
        }
    }

    internal static void ValidateLocalPath(
        string? value,
        string field,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new("path.local.required", "A local path is required.", field));
            return;
        }

        ValidateWindowsPath(value, field, allowRoot: false, "path.local", issues);
    }

    internal static void ValidateDirectoryMountPath(
        string? value,
        string field,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new("mount.target.directory.required", "A mount directory is required.", field));
            return;
        }

        ValidateWindowsPath(value, field, allowRoot: false, "mount.target.directory", issues);
    }

    private static void ValidateWindowsPath(
        string value,
        string field,
        bool allowRoot,
        string codePrefix,
        List<ValidationIssue> issues)
    {
        if (value.Length > MaximumPathLength || value.Any(IsUnsafeCharacter))
        {
            issues.Add(new($"{codePrefix}.invalid", "The path is too long or contains control characters.", field));
            return;
        }


        if (value.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            value.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            value.IndexOfAny(['"', '<', '>', '|', '*', '?']) >= 0 ||
            (value.Length > 2 && value.IndexOf(':', 2) >= 0))
        {
            issues.Add(new($"{codePrefix}.invalid", "The path uses reserved Windows syntax or characters.", field));
            return;
        }

        if (!Path.IsPathFullyQualified(value))
        {
            issues.Add(new($"{codePrefix}.absolute", "The path must be fully qualified.", field));
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(value);
            var segments = value.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment is "." or ".."))
            {
                issues.Add(new($"{codePrefix}.traversal", "The path cannot contain dot traversal segments.", field));
            }

            var root = Path.GetPathRoot(fullPath);
            if (!allowRoot && root is not null &&
                fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new($"{codePrefix}.root", "A volume root cannot be used for this path.", field));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(new($"{codePrefix}.invalid", "The path is not valid.", field));
        }
    }

    private static bool IsUnsafeCharacter(char value) => value == '\0' || value == '\r' || value == '\n' || char.IsControl(value);
}
