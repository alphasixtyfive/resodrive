using ResoDrive.Core.Domain;

namespace ResoDrive.Core.Tests;

public sealed class AutoMountPolicyExtensionsTests
{
    [Theory]
    [InlineData(MountLifecycle.Stopped, true)]
    [InlineData(MountLifecycle.Failed, true)]
    [InlineData(MountLifecycle.Starting, false)]
    [InlineData(MountLifecycle.Mounted, false)]
    [InlineData(MountLifecycle.Degraded, false)]
    [InlineData(MountLifecycle.Stopping, false)]
    [InlineData(MountLifecycle.WaitingToRestart, false)]
    public void IsAutomaticStartEligible_StartsOnlyInactiveAutomaticMounts(
        MountLifecycle lifecycle,
        bool expected)
    {
        var definition = ValidationTestData.ValidMount() with
        {
            AutoMount = AutoMountPolicy.OnApplicationStart,
        };

        Assert.Equal(expected, definition.IsAutomaticStartEligible(lifecycle));
    }

    [Fact]
    public void IsAutomaticStartEligible_RejectsDisabledAndManualMounts()
    {
        var automatic = ValidationTestData.ValidMount() with
        {
            AutoMount = AutoMountPolicy.OnApplicationStart,
        };

        Assert.False((automatic with { Enabled = false })
            .IsAutomaticStartEligible(MountLifecycle.Failed));
        Assert.False((automatic with { AutoMount = AutoMountPolicy.Never })
            .IsAutomaticStartEligible(MountLifecycle.Failed));
    }
}
