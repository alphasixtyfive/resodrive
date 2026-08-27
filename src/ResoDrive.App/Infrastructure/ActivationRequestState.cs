namespace ResoDrive.App;

/// <summary>Keeps activation intent independent from WPF startup timing.</summary>
internal sealed class ActivationRequestState
{
    private int _forceVisible;
    private int _pending;

    internal bool HasPendingRequest => Volatile.Read(ref _pending) != 0;

    internal bool ShouldHideForBackgroundStartup => Volatile.Read(ref _forceVisible) == 0;

    internal void RequestShow()
    {
        Interlocked.Exchange(ref _forceVisible, 1);
        Interlocked.Increment(ref _pending);
    }

    internal bool CanAcknowledge(
        bool windowAvailable,
        bool loaded,
        bool startupReady,
        bool closing,
        bool dispatcherLive,
        bool windowVisible) =>
        HasPendingRequest && windowAvailable && loaded && startupReady && !closing &&
        dispatcherLive && windowVisible;

    internal void CompleteRequest() => Interlocked.Decrement(ref _pending);
}
