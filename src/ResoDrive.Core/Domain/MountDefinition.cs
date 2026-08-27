namespace ResoDrive.Core.Domain;

public sealed record MountDefinition
{
    public required MountId Id { get; init; }
    public required string DisplayName { get; init; }
    public required string RemoteName { get; init; }
    public string? ConnectionHost { get; init; }
    public string RemotePath { get; init; } = string.Empty;
    public required MountTarget Target { get; init; }
    public bool Enabled { get; init; } = true;
    public AutoMountPolicy AutoMount { get; init; }
    public RestartPolicy Restart { get; init; } = RestartPolicy.Default;
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SyncJob> SyncJobs { get; init; } = Array.Empty<SyncJob>();
}

public abstract record MountTarget
{
    private MountTarget() { }

    public sealed record Drive : MountTarget
    {
        public Drive(char letter) => Letter = char.ToUpperInvariant(letter);

        public char Letter { get; }
    }

    public sealed record Directory(string Path) : MountTarget;
}

public enum AutoMountPolicy
{
    Never,
    OnApplicationStart
}

public sealed record RestartPolicy
{
    public static RestartPolicy Default { get; } = new();

    public bool Enabled { get; init; } = true;
    public int MaximumAttempts { get; init; } = 5;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaximumDelay { get; init; } = TimeSpan.FromMinutes(1);
}
