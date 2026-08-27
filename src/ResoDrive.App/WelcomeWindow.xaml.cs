using System.Windows;
using WpfBrush = System.Windows.Media.Brush;

namespace ResoDrive.App;

public partial class WelcomeWindow : Window
{
    private int _page;

    public WelcomeWindow()
    {
        InitializeComponent();
        WindowAppearance.PrepareDialog(this);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_page < 2)
        {
            _page++;
            UpdatePage();
            return;
        }

        DialogResult = true;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_page > 0)
        {
            _page--;
            UpdatePage();
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void UpdatePage()
    {
        DrivePage.Visibility = _page == 0 ? Visibility.Visible : Visibility.Collapsed;
        SyncPage.Visibility = _page == 1 ? Visibility.Visible : Visibility.Collapsed;
        RclonePage.Visibility = _page == 2 ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = _page == 0 ? Visibility.Collapsed : Visibility.Visible;
        SkipButton.Visibility = _page == 2 ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Content = _page == 2 ? "Continue" : "Next";
        Dot1.Fill = Brush(_page == 0);
        Dot2.Fill = Brush(_page == 1);
        Dot3.Fill = Brush(_page == 2);
    }

    private WpfBrush Brush(bool selected) =>
        (WpfBrush)FindResource(selected ? "AccentBrush" : "BorderBrush");
}
