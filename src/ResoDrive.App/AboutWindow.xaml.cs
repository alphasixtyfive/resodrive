using System.Diagnostics;
using System.Windows;
using WpfMessageBox = ResoDrive.App.ModernMessageBox;
using WpfWindow = System.Windows.Window;

namespace ResoDrive.App;

public partial class AboutWindow : WpfWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {ProductInfo.Version}";
        WindowAppearance.PrepareDialog(this);
    }

    private void OpenRepository_Click(object sender, RoutedEventArgs e) =>
        OpenLink(ProductInfo.RepositoryPage, "Could not open the project repository");

    private void OpenIssues_Click(object sender, RoutedEventArgs e) =>
        OpenLink(ProductInfo.IssuesPage, "Could not open the issue tracker");

    private void OpenReleases_Click(object sender, RoutedEventArgs e) =>
        OpenLink(ProductInfo.ReleasesPage, "Could not open the releases page");

    private void OpenLicense_Click(object sender, RoutedEventArgs e) =>
        OpenLink(ProductInfo.LicensePage, "Could not open the license");

    private void OpenLink(Uri destination, string title)
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo(destination.AbsoluteUri) { UseShellExecute = true }
            );
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            WpfMessageBox.Show(
                this,
                exception.Message,
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}
