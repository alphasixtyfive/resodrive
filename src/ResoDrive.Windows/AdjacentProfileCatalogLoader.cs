using ResoDrive.Core.Setup;

namespace ResoDrive.Windows;

public static class AdjacentProfileCatalogLoader
{
    private const long MaximumProfileFileBytes = 512 * 1024;

    public static SetupProfileCatalog Load(
        string? applicationDirectory = null,
        string? userFile = null)
    {
        string? userDiagnostic = null;
        if (userFile is not null || applicationDirectory is null)
        {
            var overridePath = userFile ?? new ApplicationPaths().ProfilesFile;
            if (File.Exists(overridePath))
            {
                var userCatalog = LoadFile(overridePath, ProfileCatalogSource.UserFile);
                if (userCatalog.Source == ProfileCatalogSource.UserFile)
                    return userCatalog;
                userDiagnostic = userCatalog.Diagnostic;
            }
        }

        var directory = Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory);
        var path = Path.Combine(directory, "profiles.json");
        if (!File.Exists(path))
        {
            return Empty(path, userDiagnostic);
        }

        var bundledCatalog = LoadFile(path, ProfileCatalogSource.AdjacentFile);
        return userDiagnostic is null || bundledCatalog.Source != ProfileCatalogSource.AdjacentFile
            ? bundledCatalog
            : new SetupProfileCatalog(
                bundledCatalog.Profiles,
                ProfileCatalogSource.AdjacentFile,
                bundledCatalog.SourcePath,
                Combine(userDiagnostic, "The bundled profile catalog is being used instead."));
    }

    private static SetupProfileCatalog LoadFile(string path, ProfileCatalogSource source)
    {
        try
        {
            if (new FileInfo(path).Length > MaximumProfileFileBytes)
            {
                return Empty(path, "profiles.json is too large; manual setup is being used.");
            }

            var parsed = SetupProfileCatalogJson.Parse(File.ReadAllText(path));
            if (!parsed.Succeeded || parsed.Value is null)
            {
                return Empty(
                    path,
                    $"profiles.json is invalid ({parsed.Error?.Message ?? "unknown error"}); manual setup is being used.");
            }

            return new SetupProfileCatalog(parsed.Value, source, path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Empty(path, $"profiles.json could not be read ({exception.Message}); manual setup is being used.");
        }
    }

    private static SetupProfileCatalog Empty(string path, string? diagnostic) =>
        new SetupProfileCatalog(
            [],
            ProfileCatalogSource.None,
            path,
            diagnostic);

    private static string Combine(string? first, string second) =>
        string.IsNullOrWhiteSpace(first) ? second : $"{first} {second}";
}
