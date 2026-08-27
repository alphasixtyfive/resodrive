using System.Windows;
using WpfButton = System.Windows.Controls.Button;

namespace ResoDrive.App;

public partial class MessageDialog : Window
{
    private MessageBoxResult _result;

    public MessageDialog(
        string heading,
        string message,
        MessageBoxButton buttons,
        MessageBoxImage image,
        string? affirmativeLabel = null,
        string? negativeLabel = null)
    {
        InitializeComponent();
        WindowAppearance.PrepareDialog(this);
        Title = heading;
        HeadingText.Text = heading;
        MessageText.Text = message;
        ApplySeverity(image);
        ConfigureButtons(buttons, affirmativeLabel, negativeLabel);
    }

    public MessageBoxResult Result => _result;

    private void ApplySeverity(MessageBoxImage image)
    {
        (SeverityGlyph.Text, SeverityGlyph.Foreground) = image switch
        {
            MessageBoxImage.Error => ("\uE711", StatusPalette.Error),
            MessageBoxImage.Warning => ("\uE7BA", StatusPalette.Warning),
            MessageBoxImage.Question => ("\uE897", StatusPalette.Info),
            _ => ("\uE946", StatusPalette.Info),
        };
        SeverityBadge.BorderBrush = SeverityGlyph.Foreground;
        System.Windows.Automation.AutomationProperties.SetName(
            SeverityGlyph,
            image switch
            {
                MessageBoxImage.Error => "Error",
                MessageBoxImage.Warning => "Warning",
                MessageBoxImage.Question => "Question",
                _ => "Information",
            });
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            _result = MessageBoxResult.Cancel;
            Close();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void ConfigureButtons(
        MessageBoxButton buttons,
        string? affirmativeLabel,
        string? negativeLabel)
    {
        switch (buttons)
        {
            case MessageBoxButton.OKCancel:
                AddButton("Cancel", MessageBoxResult.Cancel, accent: false);
                AddButton("OK", MessageBoxResult.OK, accent: true, isDefault: true);
                break;
            case MessageBoxButton.YesNo:
                AddButton(negativeLabel ?? "No", MessageBoxResult.No, accent: false);
                AddButton(affirmativeLabel ?? "Yes", MessageBoxResult.Yes, accent: true, isDefault: true);
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("Cancel", MessageBoxResult.Cancel, accent: false);
                AddButton("No", MessageBoxResult.No, accent: false);
                AddButton("Yes", MessageBoxResult.Yes, accent: true, isDefault: true);
                break;
            default:
                AddButton("OK", MessageBoxResult.OK, accent: true, isDefault: true);
                break;
        }
    }

    private void AddButton(string label, MessageBoxResult result, bool accent, bool isDefault = false)
    {
        var button = new WpfButton
        {
            Content = label,
            MinWidth = 80,
            Margin = ButtonsPanel.Children.Count == 0 ? new Thickness() : new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
            Style = (Style)FindResource(accent ? "AccentButton" : "ButtonStyle")
        };
        button.Click += (_, _) =>
        {
            _result = result;
            Close();
        };
        ButtonsPanel.Children.Add(button);
    }
}

public static class ModernMessageBox
{
    public static MessageBoxResult Show(
        Window owner,
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dialog = new MessageDialog(title, message, buttons, image) { Owner = owner };
        dialog.ShowDialog();
        return dialog.Result;
    }

    public static bool Confirm(
        Window owner,
        string message,
        string title,
        string confirmLabel,
        string cancelLabel = "Cancel")
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dialog = new MessageDialog(
            title,
            message,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            confirmLabel,
            cancelLabel)
        {
            Owner = owner,
        };
        dialog.ShowDialog();
        return dialog.Result == MessageBoxResult.Yes;
    }
}
