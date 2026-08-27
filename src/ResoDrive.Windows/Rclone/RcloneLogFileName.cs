using System.Text;
using ResoDrive.Core.Domain;

namespace ResoDrive.Windows;

internal static class RcloneLogFileName
{
    public static string ForMount(MountDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var target = definition.Target switch
        {
            MountTarget.Drive drive => char.ToUpperInvariant(drive.Letter).ToString(),
            MountTarget.Directory directory => Segment(Path.GetFileName(Path.TrimEndingDirectorySeparator(directory.Path))),
            _ => definition.Id.Value.ToString("N")[..8]
        };
        return $"mount-{Segment(definition.DisplayName)}-{target}.log";
    }

    public static string ForSync(MountDefinition definition, SyncJob job)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(job);
        var shortId = job.Id.Value.ToString("N")[..8];
        return $"sync-{Segment(definition.DisplayName)}-{Segment(job.DisplayName)}-{shortId}.jsonl";
    }

    private static string Segment(string? value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new StringBuilder();
        var separatorPending = false;
        foreach (var character in value?.Trim() ?? string.Empty)
        {
            if (char.IsWhiteSpace(character) || character == '-')
            {
                separatorPending = result.Length > 0;
                continue;
            }
            if (char.IsControl(character) || invalid.Contains(character))
                continue;
            if (separatorPending)
            {
                result.Append('-');
                separatorPending = false;
            }
            result.Append(character);
            if (result.Length == 48)
                break;
        }
        var segment = result.ToString().TrimEnd('.', ' ');
        return segment.Length == 0 ? "unnamed" : segment;
    }
}
