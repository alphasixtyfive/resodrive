namespace ResoDrive.Core.Domain;

public static class AutoMountPolicyExtensions
{
    /// <summary>
    /// Returns whether an inactive drive should be started by application startup or
    /// explicit runtime activation. Active and transitional drives are left untouched.
    /// </summary>
    public static bool IsAutomaticStartEligible(
        this MountDefinition definition,
        MountLifecycle lifecycle)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Enabled &&
            definition.AutoMount == AutoMountPolicy.OnApplicationStart &&
            lifecycle is MountLifecycle.Stopped or MountLifecycle.Failed;
    }
}
