using System.IO;
using System.Windows;
using Microsoft.Win32;
using ResoDrive.Core.Domain;
using ResoDrive.Core.Settings;
using ResoDrive.Core.Validation;
using WpfMessageBox = ResoDrive.App.ModernMessageBox;
using WpfWindow = System.Windows.Window;

namespace ResoDrive.App;

public partial class SyncEditorWindow : WpfWindow
{
    private sealed record SyncModeOption(SyncMode Value, string Label);

    private static readonly IReadOnlyList<SyncModeOption> Modes =
    [
        new(SyncMode.CopyFromRemote, "Copy remote to local"),
        new(SyncMode.CopyToRemote, "Copy local to remote"),
        new(SyncMode.SyncFromRemote, "Mirror remote to local"),
        new(SyncMode.SyncToRemote, "Mirror local to remote"),
    ];

    private readonly SyncJobSettings? _existing;

    public SyncEditorWindow(
        IReadOnlyList<MountSettings> mounts,
        Guid? mountId,
        SyncJobSettings? existing
    )
    {
        InitializeComponent();
        WindowAppearance.PrepareDialog(this);
        _existing = existing;
        MountBox.ItemsSource = mounts;
        ModeBox.ItemsSource = Modes;
        ModeBox.DisplayMemberPath = nameof(SyncModeOption.Label);
        MountBox.SelectedItem =
            mounts.FirstOrDefault(mount => mount.Id == mountId)
            ?? (mounts.Count > 0 ? mounts[0] : null);
        DeleteButton.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;
        Heading.Text = existing is null ? "New sync job" : "Edit sync job";
        if (existing is null)
        {
            ModeBox.SelectedItem = Modes[0];
            EnabledBox.IsChecked = true;
            IntervalBox.Text = "60";
        }
        else
        {
            NameBox.Text = existing.DisplayName;
            RemotePathBox.Text = existing.RemotePath;
            LocalPathBox.Text = existing.LocalPath;
            ModeBox.SelectedItem = Enum.TryParse<SyncMode>(existing.Mode, true, out var existingMode)
                ? Modes.FirstOrDefault(mode => mode.Value == existingMode) ?? Modes[0]
                : Modes[0];
            EnabledBox.IsChecked = existing.Enabled;
            ScheduleBox.IsChecked = existing.Schedule.Enabled;
            RunOnStartBox.IsChecked = existing.Schedule.RunOnApplicationStart;
            IntervalBox.Text = existing.Schedule.IntervalMinutes.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
            ArgumentsBox.Text = RcloneArgumentTextCodec.Format(existing.Arguments);
            MountBox.IsEnabled = false;
        }
        UpdateScheduleControls();
    }

    public MountSettings? SelectedMount => MountBox.SelectedItem as MountSettings;
    public SyncJobSettings? Value { get; private set; }
    public bool DeleteRequested { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMount is null)
        {
            WpfMessageBox.Show(
                this,
                "Choose a drive for this sync job.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }
        var scheduled = ScheduleBox.IsChecked == true;
        var interval = 60;
        if (scheduled && (!int.TryParse(IntervalBox.Text, out interval) || interval is < 5 or > 1440))
        {
            WpfMessageBox.Show(
                this,
                "The interval must be between 5 and 1440 minutes.",
                Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }
        var selectedMode = ModeBox.SelectedItem as SyncModeOption ?? Modes[0];
        var arguments = RcloneArgumentTextCodec.Parse(ArgumentsBox.Text);
        var job = new SyncJob
        {
            Id = new SyncJobId(_existing?.Id ?? Guid.NewGuid()),
            DisplayName = NameBox.Text.Trim(),
            Enabled = EnabledBox.IsChecked == true,
            RemotePath = RemotePathUtility.Normalize(RemotePathBox.Text),
            LocalPath = LocalPathBox.Text.Trim(),
            Mode = selectedMode.Value,
            Schedule = new SyncSchedule
            {
                Enabled = scheduled,
                Interval = TimeSpan.FromMinutes(interval),
                RunOnApplicationStart = RunOnStartBox.IsChecked == true,
            },
            Arguments = arguments,
        };
        var validation = new SyncJobValidator().Validate(job);
        if (!validation.IsValid)
        {
            WpfMessageBox.Show(
                this,
                string.Join(
                    Environment.NewLine,
                    validation.Issues.Select(issue => "• " + issue.Message)
                ),
                "Check sync job",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }
        var mirror = job.Mode.IsMirror();
        if (
            mirror
            && (job.Schedule.Enabled || job.Schedule.RunOnApplicationStart)
            && !WpfMessageBox.Confirm(
                this,
                $"This automatic mirror may delete destination-only files in:\n\n{MirrorDestination(selectedMode)}\n\nEnable automatic runs?",
                "Confirm automatic mirror",
                "Enable automatic runs"
            )
        )
            return;
        Value = new SyncJobSettings
        {
            Id = job.Id.Value,
            DisplayName = job.DisplayName,
            Enabled = job.Enabled,
            RemotePath = job.RemotePath,
            LocalPath = job.LocalPath,
            Mode = job.Mode.ToString(),
            Schedule = new SyncScheduleSettings
            {
                Enabled = job.Schedule.Enabled,
                IntervalMinutes = checked((int)job.Schedule.Interval.TotalMinutes),
                RunOnApplicationStart = job.Schedule.RunOnApplicationStart,
            },
            Arguments = job.Arguments.ToArray(),
        };
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (
            WpfMessageBox.Confirm(
                this,
                "Delete this sync job? No local or remote files will be deleted.",
                Title,
                "Delete sync job"
            )
        )
        {
            DeleteRequested = true;
            DialogResult = true;
        }
    }

    private void Mode_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        MirrorWarning.Visibility =
            (ModeBox.SelectedItem as SyncModeOption)?.Value.IsMirror() == true
                ? Visibility.Visible
                : Visibility.Collapsed;

    private void Schedule_Changed(object sender, RoutedEventArgs e) => UpdateScheduleControls();

    private void BrowseLocalFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose a local folder",
            Multiselect = false,
        };
        var currentPath = LocalPathBox.Text.Trim();
        if (Directory.Exists(currentPath))
        {
            dialog.InitialDirectory = Path.GetFullPath(currentPath);
        }

        if (dialog.ShowDialog(this) == true)
        {
            LocalPathBox.Text = dialog.FolderName;
            LocalPathBox.Focus();
            LocalPathBox.CaretIndex = LocalPathBox.Text.Length;
        }
    }

    private void UpdateScheduleControls()
    {
        if (!IsInitialized)
            return;
        var enabled = ScheduleBox.IsChecked == true;
        IntervalBox.IsEnabled = enabled;
        IntervalLabel.Opacity = enabled ? 1 : 0.6;
    }

    private string MirrorDestination(SyncModeOption mode) =>
        mode.Value == SyncMode.SyncFromRemote
            ? LocalPathBox.Text.Trim()
            : SelectedMount is null
                ? "the selected remote folder"
                : RemotePathUtility.Display(
                    SelectedMount.DisplayName,
                    SelectedMount.RemotePath,
                    RemotePathBox.Text.Trim());
}
