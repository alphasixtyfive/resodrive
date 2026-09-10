using System.Windows;
using System.Windows.Controls;
using ResoDrive.Core.Validation;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace ResoDrive.App.Controls;

public partial class MountOptionsControl : System.Windows.Controls.UserControl
{
    private RcloneMountOptions _options = new([]);
    private bool _loading;
    private static readonly Choice[] Modes =
    [
        new("Read and write cache (recommended)", "full"),
        new("Writes only", "writes"), new("Minimal caching", "minimal"), new("No disk cache", "off")
    ];
    private static readonly Choice[] Sizes =
    [
        new("1 GiB", "1G"), new("2 GiB", "2G"), new("5 GiB", "5G"),
        new("10 GiB", "10G"), new("20 GiB", "20G"), new("Unlimited", "off")
    ];
    private static readonly Choice[] Ages =
    [new("1 hour", "1h"), new("24 hours", "24h"), new("72 hours", "72h"), new("7 days", "7d")];

    public MountOptionsControl()
    {
        InitializeComponent();
        ModeBox.ItemsSource = Modes;
        SizeBox.ItemsSource = Sizes;
        AgeBox.ItemsSource = Ages;
        SizeBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(Options_TextChanged));
        AgeBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(Options_TextChanged));
        LoadArguments([], newMount: true);
    }

    public void LoadArguments(IReadOnlyList<string> arguments, bool newMount = false)
    {
        _loading = true;
        _options = new(arguments, newMount);
        ModeBox.SelectedValue = _options.CacheMode;
        SetChoice(SizeBox, Sizes, _options.CacheSize);
        SetChoice(AgeBox, Ages, _options.CacheAge);
        ArgumentsBox.Text = RcloneArgumentTextCodec.Format(_options.AdditionalArguments);
        _loading = false;
        UpdateHints();
    }

    public bool TryGetArguments(bool networkMode, out string[] arguments, out string? error)
    {
        var additional = RcloneArgumentTextCodec.Parse(ArgumentsBox.Text);
        if (additional.Any(RcloneMountOptions.IsControlOption))
        {
            arguments = [];
            error = "Use the caching and network-drive controls for cache and network-mode options; remove those options from Additional rclone options.";
            return false;
        }
        arguments = _options.Compose(ModeBox.SelectedValue as string ?? _options.CacheMode,
            ChoiceValue(SizeBox), ChoiceValue(AgeBox), networkMode, additional);
        var validation = RcloneArgumentPolicy.ValidateMount(arguments);
        error = validation.IsValid ? null : string.Join(Environment.NewLine, validation.Issues.Select(issue => issue.Message));
        return validation.IsValid;
    }

    private static void SetChoice(WpfComboBox box, IEnumerable<Choice> choices, string value)
    {
        var choice = choices.FirstOrDefault(item => item.Value == value);
        box.SelectedItem = choice;
        if (choice is null) box.Text = value;
    }

    private static string ChoiceValue(WpfComboBox box) =>
        box.SelectedItem is Choice choice && box.Text == choice.Label ? choice.Value : box.Text.Trim();

    private void Option_Changed(object sender, SelectionChangedEventArgs e) => UpdateHints();
    private void Options_TextChanged(object sender, TextChangedEventArgs e) => UpdateSummary();
    private void Summary_Expanded(object sender, RoutedEventArgs e) => UpdateSummary();

    private void UpdateHints()
    {
        if (_loading || ModeHint is null || SizeBox is null || AgeBox is null) return;
        var mode = ModeBox.SelectedValue as string;
        SizeBox.IsEnabled = AgeBox.IsEnabled = mode != "off";
        ModeHint.Text = mode switch
        {
            "full" => "Caches downloaded data locally for repeated access, as well as files being written.",
            "writes" when _options.UsesLegacyCacheDefault => "Legacy default. Select Read and write cache to cache downloads as well.",
            "writes" => "Files opened read-only are read from the server without a disk read cache.",
            "minimal" => "Uses disk caching for files opened for both reading and writing. Some applications may not work correctly.",
            _ => "Disables disk caching. Some applications may not work correctly. Size and retention settings are retained."
        };
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (_loading || SummaryBox is null || ArgumentsBox is null || ModeBox.SelectedValue is null) return;
        if (!TryGetArguments(_options.NetworkMode, out var arguments, out var error))
        {
            SummaryBox.Text = error;
            return;
        }
        var cache = new RcloneMountOptions(arguments);
        SummaryBox.Text = string.Join(Environment.NewLine,
            "Cache mode: " + cache.CacheMode,
            "Cache size target: " + cache.CacheSize,
            "Cache retention: " + cache.CacheAge,
            "Directory cache: " + (RcloneMountOptions.Value(arguments, "--dir-cache-time") ?? "5m (rclone default)"),
            "Connection timeout: " + (RcloneMountOptions.Value(arguments, "--contimeout") ?? "1m (rclone default)"),
            "I/O idle timeout: " + (RcloneMountOptions.Value(arguments, "--timeout") ?? "5m (rclone default)"));
    }

    private sealed record Choice(string Label, string Value);
}
