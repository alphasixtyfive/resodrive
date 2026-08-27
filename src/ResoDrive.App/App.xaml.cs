using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace ResoDrive.App;

#pragma warning disable CA1001 // WPF calls OnExit, where all process coordination handles are disposed.
public partial class App : System.Windows.Application
#pragma warning restore CA1001
{
    private static readonly string InstanceScope = CreateInstanceScope();
    // Stable compatibility identifiers: changing the product name must not allow two UI instances.
    private static readonly string InstanceMutexName = $@"Local\RDrive.Ui.Instance.{InstanceScope}";
    private static readonly string RestoreEventName = $@"Local\RDrive.Ui.Restore.{InstanceScope}";
    private Mutex? _instanceMutex;
    private EventWaitHandle? _restoreEvent;
    private RegisteredWaitHandle? _restoreRegistration;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        var startInBackground = e.Args.Any(ResoDrive.Windows.AutostartCommand.IsBackgroundArgument);
        _instanceMutex = new Mutex(
            initiallyOwned: true,
            InstanceMutexName,
            out var isFirstInstance
        );
        if (!isFirstInstance)
        {
            if (!startInBackground &&
                EventWaitHandle.TryOpenExisting(RestoreEventName, out var restoreEvent))
            {
                using (restoreEvent)
                    restoreEvent.Set();
            }
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        // Let the host load settings and queue automatic mounts while WPF builds the
        // main window and inspects optional components.
        Program.TryStartHostEarly();

        _restoreEvent = new EventWaitHandle(false, EventResetMode.AutoReset, RestoreEventName);
        _restoreRegistration = ThreadPool.RegisterWaitForSingleObject(
            _restoreEvent,
            RestoreRequested,
            null,
            Timeout.Infinite,
            executeOnlyOnce: false
        );
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        var mainWindow = new MainWindow(startInBackground);
        MainWindow = mainWindow;
        if (startInBackground)
        {
            mainWindow.ShowActivated = false;
            mainWindow.ShowInTaskbar = false;
            mainWindow.WindowState = System.Windows.WindowState.Minimized;
            mainWindow.Loaded += (_, _) => mainWindow.Hide();
            mainWindow.Show();
        }
        else
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _restoreRegistration?.Unregister(null);
        _restoreEvent?.Dispose();
        if (_instanceMutex is not null)
            _instanceMutex.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void RestoreRequested(object? state, bool timedOut)
    {
        if (!timedOut)
            Dispatcher.BeginInvoke(() =>
                (MainWindow as ResoDrive.App.MainWindow)?.RestoreFromExternalRequest()
            );
    }

    private static string CreateInstanceScope()
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory))
            .ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(directory));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
