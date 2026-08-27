using System.Text.Json;
using ResoDrive.Core.Results;

namespace ResoDrive.Core.Setup;

public enum ProfileCatalogSource { None, AdjacentFile, UserFile }

public interface ISetupProfileCatalog
{
    IReadOnlyList<SetupProfile> Profiles { get; }
    ProfileCatalogSource Source { get; }
    string? SourcePath { get; }
    string? Diagnostic { get; }
    SetupProfile? Find(string? id);
}

public sealed class SetupProfileCatalog : ISetupProfileCatalog
{
    public SetupProfileCatalog(IEnumerable<SetupProfile> profiles, ProfileCatalogSource source,
        string? sourcePath = null, string? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var copy = profiles.ToArray();
        if (copy.Length == 0 && source != ProfileCatalogSource.None)
            throw new ArgumentException("A file-based profile catalog cannot be empty.", nameof(profiles));
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in copy)
            if (!SetupProfileValidator.Validate(profile).IsValid || !ids.Add(profile.Id))
                throw new ArgumentException("The profile catalog contains an invalid or duplicate profile.", nameof(profiles));
        Profiles = Array.AsReadOnly(copy);
        Source = source;
        SourcePath = sourcePath;
        Diagnostic = diagnostic;
    }

    public IReadOnlyList<SetupProfile> Profiles { get; }
    public ProfileCatalogSource Source { get; }
    public string? SourcePath { get; }
    public string? Diagnostic { get; }
    public SetupProfile? Find(string? id) => Profiles.FirstOrDefault(
        profile => profile.Id.Equals(id?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class SetupProfileCatalogJson
{
    private const int MaximumDocumentLength = 256 * 1024;
    private const int MaximumProfileCount = 32;
    private static readonly HashSet<string> RootProperties = Set("schemaVersion", "profiles");
    private static readonly HashSet<string> ProfileProperties = Set("id", "displayName", "description", "defaultRemoteName",
        "connection", "defaultRemotePath", "defaultDriveLetter", "startWithWindowsByDefault", "mountArguments");
    private static readonly HashSet<string> WebDavProperties = Set("type", "baseUrl", "pathTemplate", "vendor");
    private static readonly HashSet<string> SftpProperties = Set("type", "host", "port", "knownHost");

    public static OperationResult<IReadOnlyList<SetupProfile>> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Result.Failure<IReadOnlyList<SetupProfile>>("profiles.empty", "profiles.json is empty.");
        if (json.Length > MaximumDocumentLength) return Result.Failure<IReadOnlyList<SetupProfile>>("profiles.too_large", "profiles.json is too large.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });
            var root = document.RootElement;
            RequireObject(root, "root");
            ValidateProperties(root, RootProperties, "root");
            var schema = ReadRequiredInt(root, "schemaVersion");
            if (schema != 2)
                return Result.Failure<IReadOnlyList<SetupProfile>>("profiles.schema", "The profiles.json schema version is not supported.");
            if (!root.TryGetProperty("profiles", out var array) || array.ValueKind != JsonValueKind.Array)
                throw new JsonException("profiles must be an array.");
            var count = array.GetArrayLength();
            if (count is < 1 or > MaximumProfileCount)
                return Result.Failure<IReadOnlyList<SetupProfile>>("profiles.count", $"profiles.json must contain between 1 and {MaximumProfileCount} profiles.");
            var profiles = new List<SetupProfile>(count);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in array.EnumerateArray())
            {
                RequireObject(element, "profile");
                var profile = ReadProfile(element);
                var validation = SetupProfileValidator.Validate(profile);
                if (!validation.IsValid)
                {
                    var issue = validation.Issues[0];
                    return Result.Failure<IReadOnlyList<SetupProfile>>(issue.Code, $"Profile '{profile.Id}': {issue.Message}");
                }
                if (!ids.Add(profile.Id))
                    return Result.Failure<IReadOnlyList<SetupProfile>>("profiles.duplicate", $"Profile id '{profile.Id}' is duplicated.");
                profiles.Add(profile);
            }
            return Result.Success<IReadOnlyList<SetupProfile>>(Array.AsReadOnly(profiles.ToArray()));
        }
        catch (JsonException exception)
        {
            return Result.Failure<IReadOnlyList<SetupProfile>>("profiles.invalid_json", exception.Message);
        }
    }

    private static SetupProfile ReadProfile(JsonElement element)
    {
        ValidateProperties(element, ProfileProperties, "profile");
        if (!element.TryGetProperty("connection", out var connectionElement)) throw new JsonException("connection is required.");
        RequireObject(connectionElement, "connection");
        var type = ReadRequiredString(connectionElement, "type", 32);
        SetupConnectionDefinition connection = type switch
        {
            "webdav" => ReadWebDav(connectionElement),
            "sftpPassword" => ReadSftp(connectionElement, SftpAuthenticationMethod.Password),
            "sftpKeyFile" => ReadSftp(connectionElement, SftpAuthenticationMethod.PrivateKey),
            _ => throw new JsonException($"Unsupported connection type '{type}'.")
        };
        return Common(element, connection);
    }

    private static WebDavConnectionDefinition ReadWebDav(JsonElement element)
    {
        ValidateProperties(element, WebDavProperties, "connection");
        return new WebDavConnectionDefinition
        {
            BaseUrl = ReadRequiredUri(element, "baseUrl"),
            PathTemplate = ReadRequiredString(element, "pathTemplate", 2_048),
            Vendor = ParseVendor(ReadRequiredString(element, "vendor", 32))
        };
    }

    private static SftpConnectionDefinition ReadSftp(
        JsonElement element,
        SftpAuthenticationMethod authentication)
    {
        ValidateProperties(element, SftpProperties, "connection");
        return new SftpConnectionDefinition
        {
            Host = ReadRequiredString(element, "host", 253),
            Port = ReadOptionalInt(element, "port", 22),
            KnownHost = ReadOptionalString(element, "knownHost", string.Empty, 24_000),
            Authentication = authentication
        };
    }

    private static SetupProfile Common(JsonElement element, SetupConnectionDefinition connection)
    {
        var drive = ReadOptionalString(element, "defaultDriveLetter", "U", 1);
        if (drive.Length != 1) throw new JsonException("defaultDriveLetter must contain one letter.");
        return new SetupProfile
        {
            Id = ReadRequiredString(element, "id", 128),
            DisplayName = ReadRequiredString(element, "displayName", 128),
            Description = ReadRequiredString(element, "description", 512),
            RemoteName = ReadRequiredString(element, "defaultRemoteName", 128),
            Connection = connection,
            DefaultRemotePath = ReadOptionalString(element, "defaultRemotePath", string.Empty, 2_048),
            DefaultDriveLetter = char.ToUpperInvariant(drive[0]),
            StartWithWindowsByDefault = ReadOptionalBool(element, "startWithWindowsByDefault", true),
            MountArguments = ReadArguments(element)
        };
    }

    private static WebDavVendor ParseVendor(string value) => value switch
    {
        "nextcloud" => WebDavVendor.Nextcloud,
        "owncloud" => WebDavVendor.Owncloud,
        "sharepoint" => WebDavVendor.SharePoint,
        "fastmail" => WebDavVendor.Fastmail,
        "other" => WebDavVendor.Other,
        _ => throw new JsonException($"Unsupported WebDAV vendor '{value}'.")
    };
    private static IReadOnlyList<string> ReadArguments(JsonElement profile)
    {
        if (!profile.TryGetProperty("mountArguments", out var element)) return Array.Empty<string>();
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > 64)
            throw new JsonException("mountArguments must be an array with no more than 64 entries.");
        return Array.AsReadOnly(element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? string.Empty : throw new JsonException("mountArguments entries must be strings.")).ToArray());
    }

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.Ordinal);
    private static void ValidateProperties(JsonElement element, HashSet<string> allowed, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name)) throw new JsonException($"Unknown {location} property '{property.Name}'.");
            if (!seen.Add(property.Name)) throw new JsonException($"Duplicate {location} property '{property.Name}'.");
        }
    }
    private static void RequireObject(JsonElement element, string location)
    { if (element.ValueKind != JsonValueKind.Object) throw new JsonException($"The {location} value must be an object."); }
    private static string ReadRequiredString(JsonElement element, string name, int maximumLength)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new JsonException($"{name} is required and must be a string.");
        var text = value.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength) throw new JsonException($"{name} is empty or too long.");
        return text;
    }
    private static string ReadOptionalString(JsonElement element, string name, string fallback, int maximumLength)
    {
        if (!element.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind != JsonValueKind.String) throw new JsonException($"{name} must be a string.");
        var text = value.GetString() ?? string.Empty;
        if (text.Length > maximumLength) throw new JsonException($"{name} is too long.");
        return text;
    }
    private static int ReadRequiredInt(JsonElement element, string name)
    { if (!element.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number)) throw new JsonException($"{name} is required and must be an integer."); return number; }
    private static int ReadOptionalInt(JsonElement element, string name, int fallback) =>
        !element.TryGetProperty(name, out var value) ? fallback : value.TryGetInt32(out var number) ? number : throw new JsonException($"{name} must be an integer.");
    private static bool ReadOptionalBool(JsonElement element, string name, bool fallback) =>
        !element.TryGetProperty(name, out var value) ? fallback : value.ValueKind switch
        { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw new JsonException($"{name} must be true or false.") };
    private static Uri ReadRequiredUri(JsonElement element, string name)
    {
        var text = ReadRequiredString(element, name, 2_048);
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) ? uri : throw new JsonException($"{name} must be an absolute URI.");
    }
}
