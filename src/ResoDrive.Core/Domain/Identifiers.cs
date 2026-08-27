namespace ResoDrive.Core.Domain;

public readonly record struct MountId(Guid Value)
{
    public static MountId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct SyncJobId(Guid Value)
{
    public static SyncJobId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
