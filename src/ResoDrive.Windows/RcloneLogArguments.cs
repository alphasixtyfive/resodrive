namespace ResoDrive.Windows;

internal static class RcloneLogArguments
{
    public static IEnumerable<string> Create(string logFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFile);
        yield return "--log-file";
        yield return logFile;
        yield return "--log-level";
        yield return "INFO";
        yield return "--log-file-max-size";
        yield return "10M";
        yield return "--log-file-max-backups";
        yield return "3";
        yield return "--log-file-max-age";
        yield return "14d";
        yield return "--log-file-compress";
    }
}
