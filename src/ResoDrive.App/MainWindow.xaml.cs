using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ResoDrive.Core.Settings;
using ResoDrive.Core.Setup;
using ResoDrive.Core.Validation;
using ResoDrive.Windows;
using WpfFrameworkElement = System.Windows.FrameworkElement;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfMessageBox = ResoDrive.App.ModernMessageBox;
using WpfWindow = System.Windows.Window;

namespace ResoDrive.App;

#pragma warning disable CA1001 // WPF owns this window's lifetime; Closed disposes all owned resources.
public partial class MainWindow : WpfWindow
#pragma warning restore CA1001
{
    private static readonly TimeSpan InitialHostProbeTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StartingHostProbeTimeout = TimeSpan.FromMilliseconds(750);
    private readonly ApplicationPaths _paths = new();
    private readonly ShellViewModel _model = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _settingsMutationGate = new(1, 1);
    private CancellationTokenSource? _rcloneOperationCancellation;
    private AtomicSettingsStore? _store;
    private ManagerSettings _settings = new();
    private bool _refreshing;
    private bool _exitRequested;
    private bool _rcloneUpdateBusy;
    private bool _rcloneMutationBusy;
    private bool _settingsSaveBusy;
    private bool _settingsClosing;
    private int _componentStatusGeneration;
    private bool _hostUnavailableReported;
    private WindowState _windowStateBeforeMinimize = WindowState.Normal;
    private readonly bool _startInBackground;
    private RcloneUpdateCheck? _rcloneUpdate;
    private bool _rcloneRepairRequested;
    private bool _rcloneRuntimeReady;
    private bool _applicationUpdateBusy;
    private ApplicationUpdateCheck? _applicationUpdate;
    private readonly TrayController _tray;

    public MainWindow(bool startInBackground = false)
    {
        _startInBackground = startInBackground;
        InitializeComponent();
        ApplicationUpdateStatusText.Text = $"Installed {ProductInfo.Version} · update not checked";
        SourceInitialized += (_, _) =>
        {
            WindowAppearance.ApplyDarkTitleBar(this);
            WindowAppearance.ConstrainToWorkArea(this, margin: 0, constrainMaximum: false);
        };
        DataContext = _model;
        _tray = new TrayController(
            Dispatcher,
            () => _model.Mounts,
            () => _model.Jobs,
            RunTrayMountAsync,
            RunTraySyncAsync,
            OpenDrive,
            RefreshTrayAsync,
            RestoreWindow,
            ExitApplication,
            exception => _model.AddLogEntry("\uE783", "Tray action failed", exception.Message, true));
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        StateChanged += MainWindow_StateChanged;
        SizeChanged += (_, _) => ApplyResponsiveNavigation();
        System.Windows.Application.Current.SessionEnding += Application_SessionEnding;
        _timer.Tick += Timer_Tick;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ShowWelcomeForNewProfile();
            var store = await Task.Run(() => new AtomicSettingsStore(_paths));
            if (_settingsClosing)
            {
                store.Dispose();
                return;
            }
            _store = store;
            await LoadSettingsAsync();
            await ReconcileAutostartAsync();
            var runtimeReady = (await new RcloneRuntimeLocator(_paths)
                .InspectAsync(_lifetimeCancellation.Token)).Succeeded;
            var setupNeeded = !File.Exists(_paths.ConfigFile);
            var setupDeferred = !runtimeReady;
            if (runtimeReady && setupNeeded && !_startInBackground)
            {
                setupDeferred = !await RunSetupAsync(firstRun: true);
            }
            else if (setupNeeded)
            {
                setupDeferred = true;
            }

            if (!await LoadAndConnectAsync(setupDeferred ? "Settings" : "Mounts"))
            {
                Close();
                return;
            }
            _timer.Start();
            await RefreshConnectionMetadataAsync();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // The window was closed while startup work was still in flight.
        }
        catch (Exception exception)
        {
            ShowError($"{ProductInfo.Name} could not start", exception.Message);
            _exitRequested = true;
            Close();
        }
    }

    private void ShowWelcomeForNewProfile()
    {
        if (_startInBackground || File.Exists(_paths.WelcomeCompletedFile) ||
            File.Exists(_paths.SettingsFile) || File.Exists(_paths.ConfigFile))
            return;

        var welcome = new WelcomeWindow { Owner = this };
        if (welcome.ShowDialog() != true)
            return;

        try
        {
            _paths.EnsureCreated();
            File.WriteAllText(_paths.WelcomeCompletedFile, "1");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _model.AddLogEntry("\uE783", "Welcome state was not saved", exception.Message, true);
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _settingsClosing = true;
        _lifetimeCancellation.Cancel();
        _componentStatusGeneration++;
        System.Windows.Application.Current.SessionEnding -= Application_SessionEnding;
        _tray.Dispose();
        _ = DisposeSettingsStoreAfterDrainAsync();
    }

    private async Task DisposeSettingsStoreAfterDrainAsync()
    {
        await _settingsMutationGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            _store?.Dispose();
            _store = null;
        }
        finally
        {
            _settingsMutationGate.Release();
        }
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            _model.AddLogEntry("\uE783", "Status refresh failed", exception.Message, true);
        }
    }

    private async Task<bool> LoadAndConnectAsync(string initialPage)
    {
        var response = await EnsureHostAsync();
        if (!_startInBackground &&
            response.ErrorCode?.Equals(
                "host.different_installation",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            var takeover = await OfferTakeControlAsync(response);
            if (takeover is null)
                return false;
            response = takeover;
        }
        _model.Load(
            _settings,
            response.Succeeded ? response.Mounts : null,
            response.Succeeded ? response.SyncJobs : null
        );
        UpdateTrayStatus();
        LoadSettingsControls();
        UpdateConnectionStatus();
        if (!response.Succeeded)
            _model.AddLogEntry(
                "\uE783",
                "Background host unavailable",
                response.ErrorMessage ?? "The host did not respond.",
                true
            );
        SelectPage(initialPage);
        return true;
    }

    private async Task<HostResponse?> OfferTakeControlAsync(HostResponse foreignHost)
    {
        if (string.IsNullOrWhiteSpace(foreignHost.HostBaseDirectory))
            return foreignHost;

        var activeDrives = foreignHost.Mounts?.Count(status =>
            status.Lifecycle is not "Stopped" and not "Failed") ?? 0;
        var activeJobs = foreignHost.SyncJobs?.Count(status =>
            status.Lifecycle.Equals("Running", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var hasActiveWork = activeDrives > 0 || activeJobs > 0;
        var workSummary = hasActiveWork
            ? $"\n\nActive work: {activeDrives} drive{(activeDrives == 1 ? string.Empty : "s")} and {activeJobs} sync job{(activeJobs == 1 ? string.Empty : "s")}."
            : string.Empty;
        var message =
            $"{ProductInfo.Name} is already running from:\n{foreignHost.HostBaseDirectory}\n\nOnly one copy can manage this account at a time.{workSummary}" +
            (hasActiveWork ? "\n\nTaking control will stop this work. Remote files are not deleted." : string.Empty);
        var confirmed = WpfMessageBox.Confirm(
            this,
            message,
            "Another copy is managing this account",
            hasActiveWork ? "Stop work and take control" : "Take control"
        );
        if (!confirmed)
            return null;

        var shutdown = await HostClient.ShutdownForeignHostAsync(
            foreignHost.HostBaseDirectory,
            hasActiveWork
        );
        if (!shutdown.Succeeded &&
            shutdown.ErrorCode?.Equals("host.work_active", StringComparison.OrdinalIgnoreCase) == true)
        {
            var stopConfirmed = WpfMessageBox.Confirm(
                this,
                "Work started in the other copy while you were deciding. Stop it and take control? Remote files are not deleted.",
                "Active work detected",
                "Stop work and take control"
            );
            if (!stopConfirmed)
                return null;
            shutdown = await HostClient.ShutdownForeignHostAsync(
                foreignHost.HostBaseDirectory,
                confirmed: true
            );
        }
        if (!shutdown.Succeeded)
        {
            ShowError(
                "Could not take control",
                shutdown.ErrorMessage ?? $"The other {ProductInfo.Name} host did not stop."
            );
            return null;
        }

        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(500);
            var response = await EnsureHostAsync();
            if (response.Succeeded ||
                response.ErrorCode?.Equals(
                    "host.different_installation",
                    StringComparison.OrdinalIgnoreCase) != true)
            {
                return response;
            }
        }

        ShowError(
            "Could not take control",
            $"The other {ProductInfo.Name} host did not stop in time. Close it from its notification-area menu and try again."
        );
        return null;
    }

    private async Task LoadSettingsAsync()
    {
        if (!await EnterSettingsMutationAsync())
        {
            throw new OperationCanceledException(_lifetimeCancellation.Token);
        }

        try
        {
            var result = await (
                _store ?? throw new InvalidOperationException("Settings are not ready.")
            ).LoadAsync();
            if (!result.Succeeded || result.Value is null)
                throw new InvalidOperationException(
                    result.Error?.Message ?? "Settings could not be loaded."
                );
            _settings = result.Value;
        }
        finally
        {
            _settingsMutationGate.Release();
        }
    }

    private async Task ReconcileAutostartAsync()
    {
        if (!_settings.Application.StartWithWindows)
            return;

        var result = await new RegistryAutostartService(CurrentExecutablePath)
            .SetEnabledAsync(true, _lifetimeCancellation.Token);
        if (!result.Succeeded)
        {
            _model.AddLogEntry(
                "\uE783",
                "Windows startup entry was not refreshed",
                result.Error?.Message ?? "The startup entry could not be updated.",
                true);
        }
    }

    private async Task<bool> EnrichConnectionHostsAsync()
    {
        if (!await EnterSettingsMutationAsync())
        {
            return false;
        }

        try
        {
            if (_settings.Mounts.All(mount => !string.IsNullOrWhiteSpace(mount.ConnectionHost)))
            {
                return false;
            }

            var metadata = await RcloneConnectionMetadataService.ReadHostsAsync(
                new RcloneRuntimeLocator(_paths).ExecutablePath,
                _paths);
            if (!metadata.Succeeded || metadata.Value is null || metadata.Value.Count == 0)
            {
                return false;
            }

            var changed = false;
            var mounts = _settings.Mounts.Select(mount =>
            {
                if (!string.IsNullOrWhiteSpace(mount.ConnectionHost) ||
                    !metadata.Value.TryGetValue(mount.RemoteName, out var host))
                {
                    return mount;
                }

                changed = true;
                return mount with { ConnectionHost = host };
            }).ToArray();
            if (!changed)
            {
                return false;
            }

            var saved = await (_store ?? throw new InvalidOperationException("Settings are not ready."))
                .SaveAsync(_settings with { Mounts = mounts }, _settings.Revision);
            if (saved.Succeeded && saved.Value is not null)
            {
                _settings = saved.Value;
                return true;
            }
            return false;
        }
        finally
        {
            _settingsMutationGate.Release();
        }
    }

    private async Task RefreshConnectionMetadataAsync()
    {
        try
        {
            if (!await EnrichConnectionHostsAsync())
                return;

            var status = await HostClient.SendAsync(new HostRequest("status"));
            _model.Load(
                _settings,
                status.Succeeded ? status.Mounts : null,
                status.Succeeded ? status.SyncJobs : null);
            UpdateTrayStatus();
        }
        catch (Exception exception)
        {
            _model.AddLogEntry(
                "\uE783",
                "Connection details unavailable",
                exception.Message,
                true);
        }
    }

    private static async Task<HostResponse> EnsureHostAsync()
    {
        var statusRequest = new HostRequest("status");
        var response = await HostClient.SendAsync(statusRequest, InitialHostProbeTimeout);
        if (
            response.Succeeded
            || !string.Equals(
                response.ErrorCode,
                "host.unavailable",
                StringComparison.OrdinalIgnoreCase
            )
        )
            return response;
        var hostPath = CurrentExecutablePath;
        if (!File.Exists(hostPath))
            return new HostResponse(
                false,
                "host.not_found",
                $"{ProductInfo.Name} was not found at '{hostPath}'."
            );
        using var hostProcess =
            await Task.Run(() => Process.Start(
                new ProcessStartInfo(hostPath, "--host")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppContext.BaseDirectory,
                }
            )) ?? throw new InvalidOperationException("The background host could not be started.");
        for (var attempt = 0; attempt < 6; attempt++)
        {
            await Task.Delay(350);
            response = await HostClient.SendAsync(statusRequest, StartingHostProbeTimeout);
            if (response.Succeeded)
                return response;
        }
        return response;
    }

    private async Task RefreshStatusAsync()
    {
        if (_refreshing)
            return;
        _refreshing = true;
        try
        {
            var response = await HostClient.SendAsync(new HostRequest("status"));
            if (response.Succeeded)
            {
                if (_hostUnavailableReported)
                {
                    _model.AddLogEntry("\uE73E", "Background host connected", "Status updates resumed");
                    _hostUnavailableReported = false;
                }
                _model.ApplyStatus(response.Mounts);
                _model.ApplySyncStatus(response.SyncJobs);
                UpdateTrayStatus();
            }
            else if (!_hostUnavailableReported)
            {
                _model.AddLogEntry(
                    "\uE783",
                    "Status delayed",
                    response.ErrorMessage ?? "Host unavailable",
                    true
                );
                _hostUnavailableReported = true;
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task<bool> SaveAndReloadAsync(ManagerSettings updated)
    {
        if (!await EnterSettingsMutationAsync())
        {
            return false;
        }

        try
        {
            return await SaveAndReloadCoreAsync(updated);
        }
        finally
        {
            _settingsMutationGate.Release();
        }
    }

    private async Task<bool> SaveAndReloadCoreAsync(ManagerSettings updated)
    {
        var definitions = new List<ResoDrive.Core.Domain.MountDefinition>();
        foreach (var mount in updated.Mounts)
        {
            var mapped = MountDefinitionMapper.ToDomain(mount);
            if (!mapped.Succeeded || mapped.Value is null)
            {
                ShowError(
                    "Invalid settings",
                    mapped.Error?.Message ?? $"Mount '{mount.DisplayName}' is invalid."
                );
                return false;
            }
            definitions.Add(mapped.Value);
        }
        var validation = new MountDefinitionValidator().ValidateCatalog(definitions);
        if (!validation.IsValid)
        {
            ShowError(
                "Invalid settings",
                string.Join(
                    Environment.NewLine,
                    validation.Issues.Select(issue => "• " + issue.Message)
                )
            );
            return false;
        }
        var previousSettings = _settings;
        var result = await (
            _store ?? throw new InvalidOperationException("Settings are not ready.")
        ).SaveAsync(updated, _settings.Revision);
        if (!result.Succeeded || result.Value is null)
        {
            ShowError(
                "Settings were not saved",
                result.Error?.Message ?? "Unknown settings error."
            );
            return false;
        }
        _settings = result.Value;
        var reload = await HostClient.SendAsync(new HostRequest("reload"));
        if (!reload.Succeeded)
        {
            var rollback = await _store.SaveAsync(previousSettings, _settings.Revision);
            if (rollback.Succeeded && rollback.Value is not null)
            {
                _settings = rollback.Value;
                await HostClient.SendAsync(new HostRequest("reload"));
            }
            ShowError(
                "Settings were not activated",
                rollback.Succeeded
                    ? $"{reload.ErrorMessage ?? "The background host rejected the settings."}\n\nThe previous settings were restored."
                    : $"{reload.ErrorMessage ?? "The background host rejected the settings."}\n\nThe previous settings could not be restored automatically."
            );
        }
        var status = reload.Succeeded
            ? await HostClient.SendAsync(new HostRequest("status"))
            : reload;
        _model.Load(
            _settings,
            status.Succeeded ? status.Mounts : null,
            status.Succeeded ? status.SyncJobs : null
        );
        UpdateTrayStatus();
        LoadSettingsControls();
        return reload.Succeeded;
    }

    private void LoadSettingsControls()
    {
        MinimizeToTrayBox.IsChecked = _settings.Application.MinimizeToTray;
        StartWithWindowsBox.IsChecked = _settings.Application.StartWithWindows;
    }

    private async Task<bool> SaveApplicationSettingsCoreAsync(ApplicationSettings application)
    {
        var result = await (
            _store ?? throw new InvalidOperationException("Settings are not ready."))
            .SaveAsync(_settings with { Application = application }, _settings.Revision);
        if (!result.Succeeded || result.Value is null)
        {
            ShowError(
                "Settings were not saved",
                result.Error?.Message ?? "Unknown settings error.");
            return false;
        }

        _settings = result.Value;
        LoadSettingsControls();
        return true;
    }

    private async void SettingsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsSaveBusy)
        {
            return;
        }

        _settingsSaveBusy = true;
        MinimizeToTrayBox.IsEnabled = false;
        StartWithWindowsBox.IsEnabled = false;
        RegistryAutostartService? autostart = null;
        bool? previousAutostart = null;
        var mutationEntered = false;
        try
        {
            mutationEntered = await EnterSettingsMutationAsync();
            if (!mutationEntered)
            {
                return;
            }

            var startWithWindows = StartWithWindowsBox.IsChecked == true;
            var autostartChanged = startWithWindows != _settings.Application.StartWithWindows;
            if (autostartChanged)
            {
                var executablePath = CurrentExecutablePath;
                autostart = new RegistryAutostartService(executablePath);
                var previous = await autostart.IsEnabledAsync();
                if (!previous.Succeeded)
                {
                    ShowError(
                        "Startup setting unavailable",
                        previous.Error?.Message ?? "The Windows startup entry could not be read."
                    );
                    return;
                }
                previousAutostart = previous.Value == true;
                var registryResult = await autostart.SetEnabledAsync(startWithWindows);
                if (!registryResult.Succeeded)
                {
                    ShowError(
                        "Startup setting was not changed",
                        registryResult.Error?.Message
                            ?? "The Windows startup entry could not be changed."
                    );
                    return;
                }
            }
            var application = _settings.Application with
            {
                MinimizeToTray = MinimizeToTrayBox.IsChecked == true,
                StartWithWindows = startWithWindows,
            };
            if (!await SaveApplicationSettingsCoreAsync(application) &&
                autostart is not null && previousAutostart.HasValue)
            {
                await autostart.SetEnabledAsync(previousAutostart.Value);
            }
        }
        catch (Exception exception)
        {
            if (autostart is not null && previousAutostart.HasValue)
                await autostart.SetEnabledAsync(previousAutostart.Value);
            ShowError("Settings were not saved", exception.Message);
        }
        finally
        {
            if (mutationEntered)
            {
                _settingsMutationGate.Release();
            }
            LoadSettingsControls();
            MinimizeToTrayBox.IsEnabled = true;
            StartWithWindowsBox.IsEnabled = true;
            _settingsSaveBusy = false;
        }
    }

    private void UpdateConnectionStatus()
    {
        if (!IsInitialized || _rcloneMutationBusy)
            return;
        StatusVisuals.ApplyPending(RcloneStatusIcon);
        RcloneStatusText.Text = "Checking rclone…";
        _rcloneRuntimeReady = false;
        AddMountButton.IsEnabled = false;
        var generation = ++_componentStatusGeneration;
        _ = ObserveComponentStatusAsync(UpdateLocalRcloneStatusAsync(generation), generation, rclone: true);
        WinFspStatusText.Text = "Checking WinFsp…";
        StatusVisuals.ApplyPending(WinFspStatusIcon);
        _ = ObserveComponentStatusAsync(UpdateWinFspStatusAsync(generation), generation, rclone: false);
    }

    private async Task ObserveComponentStatusAsync(Task inspection, int generation, bool rclone)
    {
        try
        {
            await inspection;
        }
        catch (Exception exception)
        {
            if (!IsInitialized || generation != _componentStatusGeneration)
                return;

            if (rclone)
            {
                StatusVisuals.Apply(RcloneStatusIcon, success: false, error: true);
                RcloneStatusText.Text = "rclone could not be inspected";
            }
            else
            {
                StatusVisuals.Apply(WinFspStatusIcon, success: false, error: true);
                WinFspStatusText.Text = "WinFsp could not be inspected";
            }
            _model.AddLogEntry("\uE783", "Component check failed", exception.Message, true);
        }
    }

    private async Task UpdateWinFspStatusAsync(int? generation = null)
    {
        var result = await WinFspPrerequisiteService.InspectAsync();
        if (!IsInitialized || generation is int value && value != _componentStatusGeneration)
        {
            return;
        }

        if (!result.Succeeded || result.Value?.IsInstalled != true)
        {
            StatusVisuals.Apply(WinFspStatusIcon, success: false);
            WinFspStatusText.Text = "WinFsp is not detected; sync works, but mount drives need it";
            WinFspReleasesButton.Visibility = Visibility.Visible;
            return;
        }

        StatusVisuals.Apply(WinFspStatusIcon, success: true);
        WinFspReleasesButton.Visibility = Visibility.Hidden;
        WinFspStatusText.Text = string.IsNullOrWhiteSpace(result.Value.Version)
            ? "WinFsp is installed"
            : $"WinFsp {result.Value.Version} is installed";
    }

    private async Task UpdateLocalRcloneStatusAsync(int? generation = null)
    {
        var result = await new RcloneRuntimeLocator(_paths).InspectAsync();
        if (!IsInitialized || generation is int value && value != _componentStatusGeneration)
        {
            return;
        }
        // A generated inspection is background component UI work. Runtime mutation owns
        // the component action and status controls until its commit or cancellation ends.
        if (generation.HasValue && _rcloneMutationBusy)
        {
            return;
        }

        if (!result.Succeeded || result.Value?.Version is null)
        {
            StatusVisuals.Apply(RcloneStatusIcon, success: false, error: true);
            RcloneStatusText.Text = result.Error?.Message ?? "rclone is unavailable";
            _rcloneRepairRequested = result.Error?.Code == "rclone.invalid";
            _rcloneRuntimeReady = false;
            var missing = result.Error?.Code == "rclone.not_installed";
            _rcloneUpdate = missing || _rcloneRepairRequested
                ? new RcloneUpdateCheck(string.Empty, RcloneBootstrapService.ReleaseVersion, true)
                : null;
            RcloneUpdateStatusText.Text = _rcloneRepairRequested
                ? "The managed runtime is invalid and can be repaired"
                : missing
                    ? $"Download {RcloneBootstrapService.ReleaseVersion} to continue"
                    : "Retry the component check when rclone is available";
            UpdateRcloneButtonText.Text = _rcloneRepairRequested ? "Repair" : missing ? "Download" : "Update";
            UpdateRcloneButton.IsEnabled = !_rcloneUpdateBusy && _rcloneUpdate?.UpdateAvailable == true;
            AddMountButton.IsEnabled = false;
            return;
        }

        StatusVisuals.Apply(RcloneStatusIcon, success: true);
        _rcloneRepairRequested = false;
        _rcloneRuntimeReady = true;
        AddMountButton.IsEnabled = !_rcloneMutationBusy;
        RcloneStatusText.Text = $"rclone {result.Value.Version} · Managed by {ProductInfo.Name}";
        UpdateRcloneButtonText.Text = "Update";
        if (_rcloneUpdate is null)
        {
            RcloneUpdateStatusText.Text = $"Installed {result.Value.Version} · update not checked";
        }
    }

    private async void CheckRcloneUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_rcloneUpdateBusy)
        {
            return;
        }

        SetRcloneUpdateBusy(true, "Checking for a stable update…");
        try
        {
            var result = await new RcloneUpdateService(new RcloneRuntimeLocator(_paths))
                .CheckAsync(_lifetimeCancellation.Token);
            if (!result.Succeeded || result.Value is null)
            {
                if (!_rcloneRepairRequested)
                    _rcloneUpdate = null;
                RcloneUpdateStatusText.Text = result.Error?.Message ?? "The update check failed.";
                return;
            }

            _rcloneUpdate = result.Value;
            _rcloneRepairRequested = false;
            _rcloneRuntimeReady = !string.IsNullOrEmpty(result.Value.CurrentVersion);
            var missing = string.IsNullOrEmpty(result.Value.CurrentVersion);
            UpdateRcloneButtonText.Text = missing ? "Download" : "Update";
            RcloneUpdateStatusText.Text = missing
                ? $"Download {result.Value.AvailableVersion} to continue"
                : result.Value.UpdateAvailable
                    ? $"Installed {result.Value.CurrentVersion} · {result.Value.AvailableVersion} available"
                    : $"{result.Value.CurrentVersion} is the latest stable version";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _rcloneUpdate = null;
            RcloneUpdateStatusText.Text = exception.Message;
        }
        finally
        {
            SetRcloneUpdateBusy(false);
        }
    }

    private async void CheckApplicationUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_applicationUpdateBusy)
            return;

        SetApplicationUpdateBusy(true);
        ApplicationUpdateStatusText.Text = "Checking for a stable release…";
        try
        {
            var result = await new ApplicationUpdateService()
                .CheckAsync(ProductInfo.Version, _lifetimeCancellation.Token);
            if (!result.Succeeded || result.Value is null)
            {
                _applicationUpdate = null;
                ApplicationUpdateStatusText.Text = result.Error?.Message ?? "The update check failed.";
                return;
            }

            _applicationUpdate = result.Value;
            ApplicationUpdateStatusText.Text = result.Value.UpdateAvailable
                ? $"Version {result.Value.AvailableVersion} is available"
                : $"Installed {result.Value.CurrentVersion} · latest stable release";
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            SetApplicationUpdateBusy(false);
        }
    }

    private void OpenApplicationUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_applicationUpdate?.UpdateAvailable != true)
            return;

        try
        {
            using var process = Process.Start(new ProcessStartInfo(
                _applicationUpdate.ReleasePage.AbsoluteUri)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowError("Could not open the ResoDrive release", exception.Message);
        }
    }

    private void SetApplicationUpdateBusy(bool busy)
    {
        _applicationUpdateBusy = busy;
        CheckApplicationUpdateButton.IsEnabled = !busy;
        GetApplicationUpdateButton.IsEnabled = !busy && _applicationUpdate?.UpdateAvailable == true;
    }

    private async void UpdateRclone_Click(object sender, RoutedEventArgs e)
    {
        if (_rcloneMutationBusy)
        {
            _rcloneOperationCancellation?.Cancel();
            UpdateRcloneButton.IsEnabled = false;
            RcloneUpdateStatusText.Text = "Cancelling…";
            return;
        }

        if (_rcloneUpdateBusy || _rcloneUpdate?.UpdateAvailable != true)
        {
            return;
        }

        if (_model.Mounts.Any(mount => mount.ShouldStop || mount.IsTransient) ||
            _model.Jobs.Any(job => job.IsBusy))
        {
            ShowError(
                "Stop active work first",
                $"Stop all mounted drives and running sync jobs before changing the {ProductInfo.Name} rclone runtime.");
            return;
        }

        var installing = string.IsNullOrEmpty(_rcloneUpdate.CurrentVersion);
        var repairing = _rcloneRepairRequested;
        _rcloneOperationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellation.Token);
        var operationToken = _rcloneOperationCancellation.Token;
        var progress = new Progress<RcloneBootstrapProgress>(UpdateRcloneProgress);
        SetRcloneUpdateBusy(
            true,
            $"{(repairing ? "Repairing" : installing ? "Downloading" : "Updating to")} {_rcloneUpdate.AvailableVersion}…",
            mutation: true);
        UpdateRcloneButtonText.Text = "Cancel";
        UpdateRcloneButton.IsEnabled = true;
        try
        {
            var locator = new RcloneRuntimeLocator(_paths);
            var result = _rcloneRepairRequested
                ? await RepairRcloneAsync(locator, progress, operationToken)
                : await new RcloneUpdateService(locator).UpdateAsync(progress, operationToken);
            if (!result.Succeeded || result.Value is null)
            {
                RcloneUpdateStatusText.Text = result.Error?.Message ?? "rclone could not be updated.";
                return;
            }

            _rcloneUpdate = null;
            _rcloneRepairRequested = false;
            var status = result.Value.Updated
                ? $"{(repairing ? "Repaired" : installing ? "Installed" : "Updated to")} {result.Value.CurrentVersion}"
                : $"{result.Value.CurrentVersion} is already current";
            await RefreshStatusAsync();
            await UpdateLocalRcloneStatusAsync();
            RcloneUpdateStatusText.Text = status;
            if ((installing || repairing) && !File.Exists(_paths.ConfigFile))
            {
                await RunSetupAsync(firstRun: true, hostAlreadyRunning: true);
            }
            else if (installing || repairing)
            {
                var reload = await HostClient.SendAsync(
                    new HostRequest("activate-runtime"),
                    _lifetimeCancellation.Token);
                if (!reload.Succeeded)
                {
                    RcloneUpdateStatusText.Text =
                        $"{status}. {reload.ErrorMessage ?? $"Restart {ProductInfo.Name} to activate it."}";
                }
                await RefreshStatusAsync();
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            RcloneUpdateStatusText.Text = "Component operation cancelled";
        }
        catch (Exception exception)
        {
            RcloneUpdateStatusText.Text = exception.Message;
        }
        finally
        {
            var cancelled = operationToken.IsCancellationRequested &&
                !_lifetimeCancellation.IsCancellationRequested;
            _rcloneOperationCancellation.Dispose();
            _rcloneOperationCancellation = null;
            SetRcloneUpdateBusy(false);
            UpdateRcloneButtonText.Text = repairing ? "Repair" : installing ? "Download" : "Update";
            if (cancelled)
                RcloneUpdateStatusText.Text = "Component operation cancelled";
        }
    }

    private void SetRcloneUpdateBusy(bool busy, string? status = null, bool mutation = false)
    {
        if (busy && mutation && !_rcloneMutationBusy)
        {
            // Ignore any component inspection that began before the mutation acquired UI
            // ownership; it may have observed the intentionally absent/staged executable.
            _componentStatusGeneration++;
        }
        _rcloneUpdateBusy = busy;
        _rcloneMutationBusy = busy && mutation;
        CheckRcloneUpdateButton.IsEnabled = !busy;
        UpdateRcloneButton.IsEnabled = !busy && _rcloneUpdate?.UpdateAvailable == true;
        AddMountButton.IsEnabled = !_rcloneMutationBusy && _rcloneRuntimeReady;
        RcloneDownloadProgress.Visibility = _rcloneMutationBusy
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_rcloneMutationBusy)
            RcloneDownloadProgress.Value = 0;
        if (status is not null)
        {
            RcloneUpdateStatusText.Text = status;
        }
    }

    private void UpdateRcloneProgress(RcloneBootstrapProgress progress)
    {
        if (!_rcloneMutationBusy)
            return;

        if (progress.Percentage is { } percentage)
        {
            RcloneDownloadProgress.Value = percentage;
            RcloneUpdateStatusText.Text = $"{progress.Message} · {percentage:0}%";
            return;
        }

        if (progress.BytesReceived > 0)
        {
            RcloneUpdateStatusText.Text =
                $"{progress.Message} · {progress.BytesReceived / 1024d / 1024d:0.0} MB";
            return;
        }

        if (!progress.Message.Equals("Downloading rclone", StringComparison.Ordinal))
            RcloneDownloadProgress.Value = 100;
        RcloneUpdateStatusText.Text = progress.Message + "…";
    }

    private static async Task<ResoDrive.Core.Results.OperationResult<RcloneUpdateResult>> RepairRcloneAsync(
        RcloneRuntimeLocator locator,
        IProgress<RcloneBootstrapProgress> progress,
        CancellationToken cancellationToken)
    {
        var installed = await new RcloneBootstrapService(locator).InstallAsync(
            progress,
            cancellationToken: cancellationToken);
        if (!installed.Succeeded || installed.Value?.Version is null)
        {
            var error = installed.Error!;
            return ResoDrive.Core.Results.Result.Failure<RcloneUpdateResult>(
                error.Code,
                error.Message,
                error.IsTransient);
        }
        return ResoDrive.Core.Results.Result.Success(new RcloneUpdateResult(
            string.Empty,
            installed.Value.Version,
            true));
    }

    private async Task<bool> RunSetupAsync(bool firstRun, bool hostAlreadyRunning = false)
    {
        var reservedDriveLetters = _settings.Mounts
            .Select(mount => mount.Target.DriveLetter)
            .Where(letter => letter.HasValue)
            .Select(letter => letter!.Value);
        var wizard = new SetupWindow(_paths, firstRun, reservedDriveLetters) { Owner = this };
        if (wizard.ShowDialog() != true || wizard.Result is null)
        {
            return false;
        }

        return await ApplyProvisioningResultAsync(
            wizard.Result,
            reloadHost: !firstRun || hostAlreadyRunning,
            applyStartupPreference: firstRun);
    }

    private async Task<bool> ApplyProvisioningResultAsync(
        ProfileProvisioningResult provisioning,
        bool reloadHost,
        bool applyStartupPreference)
    {
        using (provisioning)
        {
            var drive = provisioning.NewMount.Target.DriveLetter;
            var occupied = await new MountTargetInventory().GetOccupiedDriveLettersAsync();
            if (!occupied.Succeeded || occupied.Value is null)
            {
                ShowError(
                    "Drive letters unavailable",
                    occupied.Error?.Message ?? "Windows drive letters could not be checked.");
                return false;
            }
            if (drive is null || occupied.Value.Contains(char.ToUpperInvariant(drive.Value)))
            {
                ShowError(
                    "Drive letter is no longer available",
                    $"{drive}: became occupied while setup was running. Choose another drive and try again.");
                return false;
            }

            return await CommitProvisioningAsync(
                provisioning,
                reloadHost,
                applyStartupPreference);
        }
    }

    private async Task<bool> CommitProvisioningAsync(
        ProfileProvisioningResult provisioning,
        bool reloadHost,
        bool applyStartupPreference)
    {
        if (!await EnterSettingsMutationAsync())
        {
            return false;
        }

        try
        {
            return await CommitProvisioningCoreAsync(
                provisioning,
                reloadHost,
                applyStartupPreference);
        }
        finally
        {
            _settingsMutationGate.Release();
        }
    }

    private async Task<bool> CommitProvisioningCoreAsync(
        ProfileProvisioningResult provisioning,
        bool reloadHost,
        bool applyStartupPreference)
    {
        var executablePath = CurrentExecutablePath;

        var autostart = new RegistryAutostartService(executablePath);
        var previous = await autostart.IsEnabledAsync();
        if (!previous.Succeeded)
        {
            ShowError("Startup setting unavailable", previous.Error?.Message ?? "The Windows startup entry could not be read.");
            return false;
        }

        var previousSettings = _settings;
        var desired = applyStartupPreference
            ? provisioning.StartWithWindows
            : previous.Value == true;
        var filesApplied = false;
        var autostartApplied = false;
        var settingsApplied = false;
        try
        {
            provisioning.Files.Apply();
            filesApplied = true;

            if (applyStartupPreference)
            {
                var changed = await autostart.SetEnabledAsync(desired);
                if (!changed.Succeeded)
                {
                    ShowError("Setup was not completed", changed.Error?.Message ?? "The Windows startup entry could not be changed.");
                    return false;
                }
                autostartApplied = true;
            }

            var updated = previousSettings with
            {
                Application = previousSettings.Application with
                {
                    StartWithWindows = applyStartupPreference
                        ? provisioning.StartWithWindows
                        : previousSettings.Application.StartWithWindows,
                },
                Mounts = previousSettings.Mounts.Append(provisioning.NewMount).ToArray(),
            };
            var save = await (_store ?? throw new InvalidOperationException("Settings are not ready."))
                .SaveAsync(updated, previousSettings.Revision);
            if (!save.Succeeded || save.Value is null)
            {
                ShowError("Setup was not saved", save.Error?.Message ?? "The settings could not be saved.");
                return false;
            }

            _settings = save.Value;
            settingsApplied = true;
            if (reloadHost)
            {
                var reload = await HostClient.SendAsync(new HostRequest("reload"));
                if (!reload.Succeeded)
                {
                    ShowError(
                        "Setup could not be activated",
                        reload.ErrorMessage ?? "The background host rejected the new connection.");
                    return false;
                }
                _model.Load(_settings, reload.Mounts, reload.SyncJobs);
            }

            provisioning.Files.Complete();
            filesApplied = false;
            if (reloadHost && provisioning.NewMount.AutoMount.Equals(
                    "OnApplicationStart",
                    StringComparison.OrdinalIgnoreCase))
            {
                await HostClient.SendAsync(new HostRequest(
                    "start",
                    provisioning.NewMount.Id));
            }
            LoadSettingsControls();
            if (reloadHost)
            {
                await RefreshStatusAsync();
            }

            _model.AddLogEntry(
                "\uE77B",
                "Drive added",
                $"{provisioning.NewMount.DisplayName} · {provisioning.ConnectionSummary}");
            return true;
        }
        finally
        {
            if (filesApplied)
            {
                var settingsRestored = !settingsApplied || await TryRestoreSettingsAsync(previousSettings);
                if (settingsRestored)
                {
                    if (autostartApplied)
                    {
                        await autostart.SetEnabledAsync(previous.Value == true);
                    }
                    provisioning.Files.Rollback();
                    if (reloadHost && settingsApplied)
                    {
                        await HostClient.SendAsync(new HostRequest("reload"));
                    }
                }
                else
                {
                    // Keep the new config when settings cannot be restored; the files on disk
                    // remain a consistent pair and the next app start can activate them.
                    provisioning.Files.Complete();
                }
            }
        }
    }

    private async Task<bool> EnterSettingsMutationAsync()
    {
        if (_settingsClosing)
        {
            return false;
        }

        try
        {
            await _settingsMutationGate.WaitAsync(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            return false;
        }

        if (!_settingsClosing)
        {
            return true;
        }

        _settingsMutationGate.Release();
        return false;
    }

    private async Task<bool> TryRestoreSettingsAsync(ManagerSettings previousSettings)
    {
        var result = await (_store ?? throw new InvalidOperationException("Settings are not ready."))
            .SaveAsync(previousSettings, _settings.Revision);
        if (!result.Succeeded || result.Value is null)
        {
            ShowError(
                "Setup requires an app restart",
                $"The new connection was saved, but the previous settings could not be restored after activation failed. Restart {ProductInfo.Name} to activate the consistent saved configuration.");
            return false;
        }

        _settings = result.Value;
        LoadSettingsControls();
        return true;
    }

    private void OpenWinFspReleases_Click(object sender, RoutedEventArgs e) =>
        SetupWindow.OpenWinFspReleases();

    private void OpenProfilesFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var samplePath = Path.Combine(AppContext.BaseDirectory, "profiles.sample.json");
            if (!File.Exists(_paths.ProfilesFile))
            {
                if (!File.Exists(samplePath))
                {
                    WpfMessageBox.Show(
                        this,
                        $"No profile sample was found at:\n{samplePath}",
                        "Profiles file",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }
                _paths.EnsureCreated();
                File.Copy(samplePath, _paths.ProfilesFile);
            }

            var editorPath = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            var startInfo = new ProcessStartInfo(editorPath)
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(_paths.ProfilesFile);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                ShowError("Could not open profiles.json", "Windows Notepad could not be started.");
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or
                  System.ComponentModel.Win32Exception)
        {
            ShowError("Could not open profiles.json", exception.Message);
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _paths.EnsureCreated();
            var startInfo = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
            };
            startInfo.ArgumentList.Add(_paths.Logs);
            Process.Start(startInfo);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or
                  System.ComponentModel.Win32Exception)
        {
            ShowError("Could not open logs", exception.Message);
        }
    }

    private async void MountAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfFrameworkElement)?.DataContext is not MountRow row)
            return;
        try
        {
            var command = row.ShouldStop ? "stop" : "start";
            if (command == "start" && _rcloneMutationBusy)
            {
                ShowError("rclone is being updated", "Wait for the component operation to finish before mounting a drive.");
                return;
            }
            var response = await HostClient.SendAsync(new HostRequest(command, row.Id));
            if (!response.Succeeded)
                ShowError(
                    $"Could not {command} {row.Name}",
                    RcloneErrorMessage.Clean(
                        response.ErrorMessage,
                        "The host rejected the request.")
                );
            else
            {
                _model.ApplyStatus(response.Mounts);
                UpdateTrayStatus();
            }
        }
        catch (Exception exception)
        {
            ShowError("Mount action failed", exception.Message);
        }
    }

    private void MountErrorDetails_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfFrameworkElement)?.DataContext is not MountRow row ||
            string.IsNullOrWhiteSpace(row.ErrorDetail))
            return;

        ShowError($"{row.Name} could not be mounted", row.ErrorDetail);
    }

    private async void JobAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfFrameworkElement)?.DataContext is not SyncRow row)
            return;
        await ExecuteUiActionAsync("Sync action failed", () => RunSyncAsync(row));
    }

    private async Task RunSyncAsync(SyncRow row)
    {
        if (!row.IsBusy && _rcloneMutationBusy)
        {
            ShowError("rclone is being updated", "Wait for the component operation to finish before starting a sync.");
            return;
        }
        if (
            !row.IsBusy
            && row.IsMirror
            && !WpfMessageBox.Confirm(
                this,
                $"{row.Name} will mirror:\n\n{row.Route}\n\nFiles that exist only at the destination may be deleted. Run it now?",
                "Confirm mirror run",
                "Run mirror"
            )
        )
            return;
        var command = row.IsBusy ? "cancel-sync" : "run-sync";
        var response = await HostClient.SendAsync(new HostRequest(command, row.MountId, row.Id));
        if (!response.Succeeded)
        {
            ShowError(
                $"Could not {command.Replace('-', ' ')}",
                response.ErrorMessage ?? "The host rejected the request."
            );
            return;
        }
        _model.ApplySyncStatus(response.SyncJobs);
        UpdateTrayStatus();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RefreshStatusAsync();
        }
        catch (Exception exception)
        {
            ShowError("Refresh failed", exception.Message);
        }
    }

    private void OpenMount_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfFrameworkElement)?.DataContext is MountRow row)
            OpenDrive(row);
    }

    private async void AddMount_Click(object sender, RoutedEventArgs e)
    {
        if (_rcloneMutationBusy || !_rcloneRuntimeReady)
            return;
        var runtime = await new RcloneRuntimeLocator(_paths)
            .InspectAsync(_lifetimeCancellation.Token);
        if (!runtime.Succeeded)
        {
            SelectPage("Settings");
            ShowError(
                "rclone is required",
                "Download or repair the managed rclone component in Settings before adding a drive.");
            return;
        }

        await ExecuteUiActionAsync(
            "Could not add drive",
            async () => { await RunSetupAsync(firstRun: false); });
    }

    private async void Options_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfFrameworkElement)?.DataContext is not MountRow row)
            return;
        await ExecuteUiActionAsync("Could not update drive", () => EditMountAsync(row));
    }

    private async Task EditMountAsync(MountRow row)
    {
        var editor = new MountEditorWindow(
            row.Settings,
            row.Settings.RemoteName
        )
        {
            Owner = this,
        };
        if (editor.ShowDialog() != true)
            return;
        if (editor.DeleteRequested && row.ShouldStop)
        {
            var stopped = await HostClient.SendAsync(new HostRequest("stop", row.Id));
            if (!stopped.Succeeded)
            {
                ShowError(
                    "Mount was not deleted",
                    stopped.ErrorMessage ?? "The mount could not be stopped safely."
                );
                return;
            }
        }
        var mounts = editor.DeleteRequested
            ? _settings.Mounts.Where(mount => mount.Id != row.Id).ToArray()
            : _settings
                .Mounts.Select(mount => mount.Id == row.Id ? editor.Value ?? mount : mount)
                .ToArray();
        await SaveAndReloadAsync(_settings with { Mounts = mounts });
    }

    private async void NewJob_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteUiActionAsync("Could not add sync job", AddSyncJobAsync);
    }

    private async Task AddSyncJobAsync()
    {
        if (_settings.Mounts.Count == 0)
        {
            ShowError("No drives", "Add a drive before creating a sync job.");
            return;
        }
        var editor = new SyncEditorWindow(_settings.Mounts, null, null) { Owner = this };
        if (editor.ShowDialog() != true || editor.Value is null || editor.SelectedMount is null)
            return;
        var id = editor.SelectedMount.Id;
        var mounts = _settings
            .Mounts.Select(mount =>
                mount.Id == id
                    ? mount with
                    {
                        SyncJobs = mount.SyncJobs.Append(editor.Value).ToArray(),
                    }
                    : mount
            )
            .ToArray();
        await SaveAndReloadAsync(_settings with { Mounts = mounts });
    }

    private async void JobOptions_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as WpfFrameworkElement)?.DataContext is not SyncRow row)
            return;
        await ExecuteUiActionAsync("Could not update sync job", () => EditSyncJobAsync(row));
    }

    private async Task EditSyncJobAsync(SyncRow row)
    {
        if (row.IsBusy)
        {
            ShowError("Job is running", "Stop this sync job before editing or deleting it.");
            return;
        }
        var editor = new SyncEditorWindow(_settings.Mounts, row.MountId, row.Settings)
        {
            Owner = this,
        };
        if (editor.ShowDialog() != true)
            return;
        var mounts = _settings
            .Mounts.Select(mount =>
                mount.Id != row.MountId
                    ? mount
                    : mount with
                    {
                        SyncJobs = editor.DeleteRequested
                            ? mount.SyncJobs.Where(job => job.Id != row.Id).ToArray()
                            : mount
                                .SyncJobs.Select(job =>
                                    job.Id == row.Id ? editor.Value ?? job : job
                                )
                                .ToArray(),
                    }
            )
            .ToArray();
        await SaveAndReloadAsync(_settings with { Mounts = mounts });
    }

    private async Task ExecuteUiActionAsync(string heading, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ShowError(heading, exception.Message);
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfRadioButton { Tag: string page })
            SelectPage(page);
    }

    private void SelectPage(string page)
    {
        MountsPage.Visibility = page == "Mounts" ? Visibility.Visible : Visibility.Collapsed;
        JobsPage.Visibility = page == "Jobs" ? Visibility.Visible : Visibility.Collapsed;
        LogPage.Visibility = page == "Log" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[] { MountsNav, JobsNav, LogNav, SettingsNav })
            button.IsChecked = Equals(button.Tag, page);
    }

    private void ApplyResponsiveNavigation()
    {
        var compact = ActualWidth < 800;
        NavColumn.Width = new GridLength(compact ? 56 : 176);
        BrandPanel.HorizontalAlignment = compact
            ? System.Windows.HorizontalAlignment.Center
            : System.Windows.HorizontalAlignment.Left;
        BrandPanel.Margin = compact ? new Thickness(0) : new Thickness(12, 0, 0, 0);
        foreach (var button in new[] { MountsNav, JobsNav, LogNav, SettingsNav })
        {
            button.HorizontalContentAlignment = compact
                ? System.Windows.HorizontalAlignment.Center
                : System.Windows.HorizontalAlignment.Stretch;
            button.Padding = compact ? new Thickness(0) : new Thickness(12, 0, 0, 0);
        }
        foreach (var label in new[] { Brand, MountsLabel, JobsLabel, LogLabel, SettingsLabel })
            label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        MountsNav.ToolTip = compact ? "Drives" : null;
        JobsNav.ToolTip = compact ? "Sync" : null;
        LogNav.ToolTip = compact ? "Log" : null;
        SettingsNav.ToolTip = compact ? "Settings" : null;
    }

    private void OpenDrive(MountRow row)
    {
        if (row.Drive == '?')
            return;
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo($"{row.Drive}:\\") { UseShellExecute = true }
            );
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowError("Could not open drive", exception.Message);
        }
    }

    private async Task<TrayActionResult> RunTrayMountAsync(MountRow row)
    {
        var command = row.ShouldStop ? "stop" : "start";
        if (command == "start" && _rcloneMutationBusy)
            return TrayActionResult.Failure("rclone is being updated", "Try again when the component operation finishes.");
        var response = await HostClient.SendAsync(new HostRequest(command, row.Id));
        if (!response.Succeeded)
        {
            return TrayActionResult.Failure(
                $"{row.Name} failed",
                RcloneErrorMessage.Clean(
                    response.ErrorMessage,
                    "The background host rejected the request."));
        }

        _model.ApplyStatus(response.Mounts);
        UpdateTrayStatus();
        var updated = _model.Mounts.FirstOrDefault(mount => mount.Id == row.Id);
        return TrayActionResult.Success(
            row.Name,
            updated?.StatusText ?? $"The {command} request was accepted.");
    }

    private async Task<TrayActionResult> RunTraySyncAsync(SyncRow row)
    {
        if (!row.IsBusy && _rcloneMutationBusy)
            return TrayActionResult.Failure("rclone is being updated", "Try again when the component operation finishes.");
        if (!row.IsBusy && row.IsMirror)
        {
            RestoreWindow();
            SelectPage("Jobs");
            return TrayActionResult.SilentSuccess();
        }

        var command = row.IsBusy ? "cancel-sync" : "run-sync";
        var response = await HostClient.SendAsync(new HostRequest(command, row.MountId, row.Id));
        if (!response.Succeeded)
        {
            return TrayActionResult.Failure(
                $"{row.Name} failed",
                response.ErrorMessage ?? "The background host rejected the request.");
        }

        _model.ApplySyncStatus(response.SyncJobs);
        UpdateTrayStatus();
        var updated = _model.Jobs.FirstOrDefault(job => job.Id == row.Id);
        return TrayActionResult.Success(row.Name, updated?.Result ?? "The request was accepted.");
    }

    private async Task<TrayActionResult> RefreshTrayAsync()
    {
        var response = await HostClient.SendAsync(new HostRequest("status"));
        if (!response.Succeeded)
        {
            return TrayActionResult.Failure(
                "Refresh failed",
                response.ErrorMessage ?? "The background host is unavailable.");
        }

        _model.ApplyStatus(response.Mounts);
        _model.ApplySyncStatus(response.SyncJobs);
        UpdateTrayStatus();
        return TrayActionResult.SilentSuccess();
    }

    private void UpdateTrayStatus() => _tray.UpdateStatus(
        _model.Mounts.Count(mount => mount.IsMounted),
        _model.Jobs.Count(job => job.IsBusy));

    private void OpenAbout_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private async void ExitApplication()
    {
        if (_exitRequested)
        {
            return;
        }

        _exitRequested = true;
        _timer.Stop();
        try
        {
            var status = await HostClient.SendAsync(
                new HostRequest("status"),
                TimeSpan.FromMilliseconds(500));
            if (!status.Succeeded &&
                (status.ErrorCode is not "host.unavailable" || IsInstalledHostProcessRunning()))
            {
                _exitRequested = false;
                _timer.Start();
                RestoreWindow();
                ShowError(
                    $"{ProductInfo.Name} could not exit safely",
                    status.ErrorMessage ?? "The background host did not confirm its state.");
                return;
            }
            var activeMounts = status.Mounts?.Count(mount =>
                mount.Lifecycle is not "Stopped" and not "Failed") ?? 0;
            var activeSyncs = status.SyncJobs?.Count(job => job.Lifecycle == "Running") ?? 0;
            var confirmed = activeMounts == 0 && activeSyncs == 0;
            if (!confirmed)
            {
                RestoreWindow();
                var work = string.Join(
                    " and ",
                    new[]
                    {
                        activeMounts == 0 ? null : $"{activeMounts} mounted drive{(activeMounts == 1 ? string.Empty : "s")}",
                        activeSyncs == 0 ? null : $"{activeSyncs} running sync job{(activeSyncs == 1 ? string.Empty : "s")}"
                    }.Where(value => value is not null));
                confirmed = WpfMessageBox.Show(
                    this,
                    $"Exiting will stop {work}. Continue?",
                    $"Exit {ProductInfo.Name}?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.Yes;
            }

            if (!confirmed)
            {
                _exitRequested = false;
                _timer.Start();
                return;
            }

            var shutdown = await HostClient.SendAsync(
                new HostRequest("shutdown", Confirmed: true),
                TimeSpan.FromSeconds(1));
            if (!shutdown.Succeeded &&
                (shutdown.ErrorCode is not "host.unavailable" || IsInstalledHostProcessRunning()))
            {
                _exitRequested = false;
                _timer.Start();
                ShowError($"{ProductInfo.Name} could not exit", shutdown.ErrorMessage ?? "The background host rejected the request.");
                return;
            }
        }
        catch (Exception exception)
        {
            _exitRequested = false;
            _timer.Start();
            ShowError(
                $"{ProductInfo.Name} could not exit",
                $"The background host could not be checked safely. {ProductInfo.Name} is still running.\n\n{exception.Message}");
        }
        finally
        {
            if (_exitRequested)
            {
                Close();
            }
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.Application.MinimizeToTray)
        {
            HideToTray();
            return;
        }

        if (WindowState is WindowState.Normal or WindowState.Maximized)
        {
            _windowStateBeforeMinimize = WindowState;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_exitRequested && _settings.Application.MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void Application_SessionEnding(object? sender, SessionEndingCancelEventArgs e) =>
        _exitRequested = true;

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void RestoreWindow()
    {
        ShowInTaskbar = true;
        Show();
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        WindowAppearance.Restore(handle);
        WindowState = _windowStateBeforeMinimize == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;
        Activate();
        WindowAppearance.BringToForeground(handle);
        Focus();
    }

    private static bool IsInstalledHostProcessRunning()
    {
        var currentId = Environment.ProcessId;
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
            return true;

        try
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable));
            try
            {
                foreach (var process in processes)
                {
                    if (process.Id != currentId &&
                        process.MainModule?.FileName is { } candidate &&
                        string.Equals(
                            Path.GetFullPath(candidate),
                            Path.GetFullPath(executable),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception or
            NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    public void RestoreFromExternalRequest() => RestoreWindow();

    private static string CurrentExecutablePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "resodrive.exe");

    private void ShowError(string title, string message) =>
        WpfMessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
