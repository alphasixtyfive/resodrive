namespace ResoDrive.App.Tests;

public sealed class ActivationRequestStateTests
{
    [Fact]
    public void NewBackgroundInstance_CanHideUntilShowIsRequested()
    {
        var state = new ActivationRequestState();

        Assert.True(state.ShouldHideForBackgroundStartup);
        Assert.False(state.HasPendingRequest);
    }

    [Fact]
    public void ShowRequest_PreventsLaterBackgroundHide()
    {
        var state = new ActivationRequestState();

        state.RequestShow();
        Assert.False(state.ShouldHideForBackgroundStartup);
        Assert.True(state.HasPendingRequest);

        state.CompleteRequest();
        Assert.False(state.ShouldHideForBackgroundStartup);
        Assert.False(state.HasPendingRequest);
    }

    [Fact]
    public void CompletingOneOfTwoRequests_LeavesTheOtherPending()
    {
        var state = new ActivationRequestState();
        state.RequestShow();
        state.RequestShow();

        state.CompleteRequest();

        Assert.True(state.HasPendingRequest);
        state.CompleteRequest();
        Assert.False(state.HasPendingRequest);
    }

    [Fact]
    public void ShowRequest_CannotBeAcknowledgedBeforeWindowExistsAndStartupIsReady()
    {
        var state = new ActivationRequestState();
        state.RequestShow();

        Assert.False(state.CanAcknowledge(
            windowAvailable: false,
            loaded: false,
            startupReady: false,
            closing: false,
            dispatcherLive: true,
            windowVisible: false));
        Assert.False(state.CanAcknowledge(
            windowAvailable: true,
            loaded: true,
            startupReady: false,
            closing: false,
            dispatcherLive: true,
            windowVisible: true));
        Assert.False(state.CanAcknowledge(
            windowAvailable: true,
            loaded: true,
            startupReady: true,
            closing: false,
            dispatcherLive: true,
            windowVisible: false));
        Assert.True(state.CanAcknowledge(
            windowAvailable: true,
            loaded: true,
            startupReady: true,
            closing: false,
            dispatcherLive: true,
            windowVisible: true));
        Assert.True(state.HasPendingRequest);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ShowRequest_IsRejectedWhileClosingOrDispatcherUnavailable(
        bool closing,
        bool dispatcherLive,
        bool expected)
    {
        var state = new ActivationRequestState();
        state.RequestShow();

        Assert.Equal(expected, state.CanAcknowledge(
            windowAvailable: true,
            loaded: true,
            startupReady: true,
            closing,
            dispatcherLive,
            windowVisible: true));
    }
}
