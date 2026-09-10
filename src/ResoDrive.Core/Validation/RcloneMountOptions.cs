namespace ResoDrive.Core.Validation;

/// <summary>Projects stored mount arguments into controls without changing inherited defaults.</summary>
public sealed class RcloneMountOptions
{
    public const string CacheModeOption = "--vfs-cache-mode";
    public const string CacheSizeOption = "--vfs-cache-max-size";
    public const string CacheAgeOption = "--vfs-cache-max-age";
    public const string LegacyCacheMode = "writes";
    private static readonly string[] ControlOptions =
        [CacheModeOption, CacheSizeOption, CacheAgeOption, "--network-mode"];
    private readonly string[] _original;

    public RcloneMountOptions(IReadOnlyList<string> arguments, bool newMount = false)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _original = newMount ? ForNewMount(arguments) : arguments.ToArray();
        CacheMode = (Value(_original, CacheModeOption) ?? LegacyCacheMode).ToLowerInvariant();
        CacheSize = Value(_original, CacheSizeOption) ?? "off";
        CacheAge = Value(_original, CacheAgeOption) ?? "1h";
        UsesLegacyCacheDefault = Value(_original, CacheModeOption) is null;
        NetworkMode = HasOption(_original, "--network-mode");
        AdditionalArguments = WithoutControls(_original);
    }

    public string CacheMode { get; }
    public string CacheSize { get; }
    public string CacheAge { get; }
    public bool UsesLegacyCacheDefault { get; }
    public bool NetworkMode { get; }
    public IReadOnlyList<string> AdditionalArguments { get; }

    public static string[] ForNewMount(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return HasOption(arguments, CacheModeOption)
            ? arguments.ToArray()
            : [.. arguments, CacheModeOption + "=full"];
    }

    public static bool IsControlOption(string argument) =>
        ControlOptions.Contains(Name(argument), StringComparer.OrdinalIgnoreCase);

    public static bool HasOption(IEnumerable<string> arguments, string option) =>
        arguments.Any(argument => argument is not null && Name(argument).Equals(option, StringComparison.OrdinalIgnoreCase));

    public static string? Value(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var token = arguments[index];
            if (!Name(token).Equals(option, StringComparison.OrdinalIgnoreCase)) continue;
            var separator = token.IndexOf('=');
            return separator >= 0 ? token[(separator + 1)..] :
                index + 1 < arguments.Count ? arguments[index + 1] : null;
        }
        return null;
    }

    public string[] Compose(string cacheMode, string cacheSize, string cacheAge,
        bool networkMode, IReadOnlyList<string> additionalArguments)
    {
        ArgumentNullException.ThrowIfNull(additionalArguments);
        // Preserve both spelling and omission on an untouched save, including legacy defaults.
        var replacements = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (cacheMode != CacheMode) replacements[CacheModeOption] = cacheMode;
        if (cacheSize != CacheSize) replacements[CacheSizeOption] = cacheSize;
        if (cacheAge != CacheAge) replacements[CacheAgeOption] = cacheAge;
        if (networkMode != NetworkMode) replacements["--network-mode"] = networkMode ? string.Empty : null;
        var sameAdditional = RcloneArgumentTextCodec.Format(AdditionalArguments) ==
            RcloneArgumentTextCodec.Format(additionalArguments);
        var result = new List<string>();
        for (var index = 0; index < _original.Length; index++)
        {
            var token = _original[index];
            var name = Name(token);
            var hasSeparateValue = !token.Contains('=') && index + 1 < _original.Length &&
                !_original[index + 1].StartsWith("--", StringComparison.Ordinal);
            if (replacements.ContainsKey(name) || (!sameAdditional && !IsControlOption(token)))
            {
                if (hasSeparateValue) index++;
                continue;
            }
            result.Add(token);
            if (hasSeparateValue) result.Add(_original[++index]);
        }
        foreach (var (name, value) in replacements)
            if (value is not null) result.Add(value.Length == 0 ? name : name + "=" + value);
        if (!sameAdditional) result.AddRange(additionalArguments);
        return result.ToArray();
    }

    private static string Name(string token)
    {
        var separator = token.IndexOf('=');
        return separator < 0 ? token : token[..separator];
    }

    private static string[] WithoutControls(string[] arguments)
    {
        var result = new List<string>();
        for (var index = 0; index < arguments.Length; index++)
        {
            var token = arguments[index];
            if (!IsControlOption(token)) { result.Add(token); continue; }
            if (!token.Contains('=') && index + 1 < arguments.Length &&
                !arguments[index + 1].StartsWith("--", StringComparison.Ordinal)) index++;
        }
        return result.ToArray();
    }
}
