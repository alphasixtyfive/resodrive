using System.Windows;

namespace ResoDrive.App;

internal static class WindowRestoration
{
    internal static void PrepareForShow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        // WPF rejects showing a maximized window when ShowActivated is false. A
        // background launch deliberately sets that flag to false, so reset it
        // before making the window visible again. Normalize a hidden maximized
        // window as an additional guard; the caller reapplies the saved state
        // after Show has created/restored the native window.
        window.ShowActivated = true;
        window.ShowInTaskbar = true;
        if (!window.IsVisible && window.WindowState == WindowState.Maximized)
            window.WindowState = WindowState.Normal;
    }
}
