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
    private const string RecommendedCache = "Standard (recommended)";
    private const string MinimalCache = "Minimal disk use";
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
        CacheBox.Items.Add(RecommendedCache);
        CacheBox.Items.Add(MinimalCache);
        DeleteButton.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;
        Heading.Text = existing is null ? "Add drive" : "Edit drive";
        ConnectionText.Text = $"Using the {_remoteName} storage connection.";

        if (existing is null)
        {
            CacheBox.SelectedItem = RecommendedCache;
            EnabledBox.IsChecked = true;
            RestartBox.IsChecked = true;
            AttemptsBox.Text = "5";
        }
        else
        {
            NameBox.Text = existing.DisplayName;
            RemotePathBox.Text = existing.RemotePath;
            AutoMountBox.IsChecked =
                existing.AutoMount.Equals("OnApplicationStart", StringComparison.OrdinalIgnoreCase) ||
                existing.AutoMount.Equals("OnUserSignIn", StringComparison.OrdinalIgnoreCase);
            EnabledBox.IsChecked = existing.Enabled;
            RestartBox.IsChecked = existing.Restart.Enabled;
            AttemptsBox.Text = existing.Restart.MaximumAttempts.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            CacheBox.SelectedItem = UsesMinimalCache(existing.Arguments)
                ? MinimalCache
                : RecommendedCache;
            NetworkModeBox.IsChecked = HasOption(existing.Arguments, "--network-mode");
            ArgumentsBox.Text = RcloneArgumentTextCodec.Format(
                RemoveManagedArguments(existing.Arguments));
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
        var attempts = _existing?.Restart.MaximumAttempts ?? 5;
        if (reconnect &&
            (!int.TryParse(AttemptsBox.Text, out attempts) || attempts is < 0 or > 100))
        {
            WpfMessageBox.Show(
                this,
                "Reconnect attempts must be between 0 and 100.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var managedArguments = new List<string>
        {
            CacheBox.SelectedItem?.ToString() == MinimalCache
                ? "--vfs-cache-mode=minimal"
                : "--vfs-cache-mode=full",
        };
        if (NetworkModeBox.IsChecked == true)
        {
            managedArguments.Add("--network-mode");
        }
        var arguments = managedArguments
            .Concat(RcloneArgumentTextCodec.Parse(ArgumentsBox.Text))
            .ToArray();
        var argumentValidation = RcloneArgumentPolicy.ValidateMount(arguments);
        if (!argumentValidation.IsValid)
        {
            WpfMessageBox.Show(
                this,
                string.Join(
                    Environment.NewLine,
                    argumentValidation.Issues.Select(issue => "• " + issue.Message)),
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

    private static bool UsesMinimalCache(string[] arguments)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index].Equals("--vfs-cache-mode=minimal", StringComparison.OrdinalIgnoreCase) ||
                arguments[index].Equals("--vfs-cache-mode", StringComparison.OrdinalIgnoreCase) &&
                index + 1 < arguments.Length &&
                arguments[index + 1].Equals("minimal", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOption(IEnumerable<string> arguments, string option) =>
        arguments.Any(argument =>
            argument.Equals(option, StringComparison.OrdinalIgnoreCase) ||
            argument.StartsWith(option + "=", StringComparison.OrdinalIgnoreCase));

    private static string[] RemoveManagedArguments(string[] arguments)
    {
        var result = new List<string>(arguments.Length);
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument.StartsWith("--vfs-cache-mode=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (argument.Equals("--network-mode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (argument.Equals("--vfs-cache-mode", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < arguments.Length &&
                    !arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    index++;
                }
                continue;
            }

            result.Add(argument);
        }

        return result.ToArray();
    }
}
