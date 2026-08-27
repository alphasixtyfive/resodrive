namespace ResoDrive.Core.Domain;

public static class SyncModeExtensions
{
    public static bool IsSupported(this SyncMode mode) => mode is
        SyncMode.CopyToRemote or SyncMode.CopyFromRemote or
        SyncMode.SyncToRemote or SyncMode.SyncFromRemote;

    public static bool IsMirror(this SyncMode mode) => mode is
        SyncMode.SyncToRemote or SyncMode.SyncFromRemote;

    public static bool IsFromRemote(this SyncMode mode) => mode is
        SyncMode.CopyFromRemote or SyncMode.SyncFromRemote;
}
