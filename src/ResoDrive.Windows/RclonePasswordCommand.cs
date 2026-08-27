namespace ResoDrive.Windows;

internal static class RclonePasswordCommand
{
    public static string Create(string? protectedSecretFile = null)
    {
        var executable = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "resodrive.exe");
        return protectedSecretFile is null
            ? $"\"{executable}\" password"
            : $"\"{executable}\" password-file \"{Path.GetFullPath(protectedSecretFile)}\"";
    }
}
