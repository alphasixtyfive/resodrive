using System.IO;
using System.Windows;
using ResoDrive.Core.Domain;
using ResoDrive.Core.Settings;
using ResoDrive.Core.Validation;
using WpfMessageBox = ResoDrive.App.ModernMessageBox;
using WpfWindow = System.Windows.Window;

namespace ResoDrive.App;

public partial class MountEditorWindow : WpfWindow
{
    private static readonly TimeSpan DriveInventoryTimeout = TimeSpan.FromSeconds(5);

    private readonly MountSettings? _existing;
    private readonly char? _currentDrive;
    private readonly string _remoteName;

    public MountEditorWindow(MountSettings? existing, string remoteName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        InitializeComponent();
        WindowAppearance.PrepareDialog(this);
        _existing = existing;
        _currentDrive = existing?.Target.DriveLetter;
        _remoteName = remoteName.Trim().TrimEnd(':');

        DriveBox.IsEnabled = false;
        SaveButton.IsEnabled = false;
        Loaded += MountEditorWindow_Loaded;
        OptionsEditor.LoadArguments(existing?.Arguments ?? [], newMount: existing is null);
        DeleteButton.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;
        Heading.Text = existing is null ? "Add drive" : "Edit drive";
        ConnectionText.Text = $"Using the {_remoteName} storage connection.";

        if (existing is null)
        {
            EnabledBox.IsChecked = true;
            RestartBox.IsChecked = true;
            AttemptsBox.Text = "0";
        }
        else
        {
            NameBox.Text = existing.DisplayName;
            RemotePathBox.Text = existing.RemotePath;
            AutoMountBox.IsChecked =
                existing.AutoMount.Equals("OnApplicationStart", StringComparison.OrdinalIgnoreCase);
            EnabledBox.IsChecked = existing.Enabled;
            RestartBox.IsChecked = existing.Restart.Enabled;
            AttemptsBox.Text = existing.Restart.MaximumAttempts.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            NetworkModeBox.IsChecked = RcloneMountOptions.HasOption(existing.Arguments, "--network-mode");
        }

        UpdateRestartControls();
    }

    public MountSettings? Value { get; private set; }
    public bool DeleteRequested { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || DriveBox.SelectedItem is not char drive)
        {
            WpfMessageBox.Show(
                this,
                "Enter a name and choose a free drive letter.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var path = RemotePathUtility.Normalize(RemotePathBox.Text);
        if (!RemotePathUtility.IsWellFormed(path))
        {
            WpfMessageBox.Show(
                this,
                "Folder paths may start with one forward slash, but cannot contain backslashes, repeated slashes, or dot traversal segments.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var reconnect = RestartBox.IsChecked == true;
        var attempts = _existing?.Restart.MaximumAttempts ?? 0;
        if (reconnect &&
            (!int.TryParse(AttemptsBox.Text, out attempts) || attempts is < 0 or > 100))
        {
            WpfMessageBox.Show(
                this,
                "Reconnect attempts must be between 0 and 100. Use 0 to keep trying.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!OptionsEditor.TryGetArguments(NetworkModeBox.IsChecked == true, out var arguments, out var argumentError))
        {
            WpfMessageBox.Show(
                this,
                argumentError ?? "Check the mount options.",
                "Invalid advanced options",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Value = new MountSettings
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            DisplayName = NameBox.Text.Trim(),
            RemoteName = _remoteName,
            ConnectionHost = _existing?.ConnectionHost,
            ConnectionType = _existing?.ConnectionType,
            RemotePath = path,
            Target = new MountTargetSettings { Kind = "drive", DriveLetter = drive },
            Enabled = EnabledBox.IsChecked == true,
            AutoMount = AutoMountBox.IsChecked == true ? "OnApplicationStart" : "Never",
            Restart = (_existing?.Restart ?? new RestartSettings()) with
            {
                Enabled = reconnect,
                MaximumAttempts = attempts,
            },
            Arguments = arguments,
            SyncJobs = _existing?.SyncJobs ?? [],
        };
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (WpfMessageBox.Confirm(
                this,
                "Delete this drive? Remote data will not be changed.",
                Title,
                "Delete drive"))
        {
            DeleteRequested = true;
            DialogResult = true;
        }
    }

    private void Restart_Changed(object sender, RoutedEventArgs e) => UpdateRestartControls();

    private void UpdateRestartControls()
    {
        if (AttemptsBox is not null)
        {
            AttemptsBox.IsEnabled = RestartBox.IsChecked == true;
        }
    }

    private async void MountEditorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var occupied = await Task.Run(GetOccupiedDriveLetters)
                .WaitAsync(DriveInventoryTimeout);
            if (!IsLoaded)
                return;

            PopulateDrives(occupied, _currentDrive);
        }
        catch (Exception exception)
        {
            if (!IsLoaded)
                return;

            DriveBox.ToolTip = "Windows drive letters could not be checked.";
            DriveStatusText.Text = "Unavailable";
            DriveStatusText.ToolTip = exception.Message;
            if (_currentDrive is char current)
            {
                DriveBox.Items.Add(current);
                DriveBox.SelectedItem = current;
                DriveBox.IsEnabled = true;
                SaveButton.IsEnabled = true;
            }
        }
    }

    private static HashSet<char> GetOccupiedDriveLetters() => DriveInfo.GetDrives()
        .Select(drive => char.ToUpperInvariant(drive.Name[0]))
        .ToHashSet();

    private void PopulateDrives(HashSet<char> occupied, char? currentDrive)
    {

        foreach (var letter in Enumerable.Range('D', 'Z' - 'D' + 1).Select(value => (char)value))
        {
            if (!occupied.Contains(letter) || letter == currentDrive)
            {
                DriveBox.Items.Add(letter);
            }
        }

        if (DriveBox.Items.Count == 0)
        {
            DriveBox.ToolTip = "No free drive letters are available.";
            DriveStatusText.Text = "None available";
            SaveButton.IsEnabled = false;
            return;
        }

        if (currentDrive is char current && DriveBox.Items.Contains(current))
        {
            DriveBox.SelectedItem = current;
        }
        DriveBox.ToolTip = null;
        DriveBox.IsEnabled = true;
        DriveStatusText.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = true;
    }

}
