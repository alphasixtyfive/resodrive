using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ResoDrive.App.Controls;

public sealed class ScrollBarGutter : FrameworkElement
{
    public static readonly DependencyProperty ScrollOwnerProperty = DependencyProperty.Register(
        nameof(ScrollOwner),
        typeof(FrameworkElement),
        typeof(ScrollBarGutter),
        new PropertyMetadata(null, OnScrollOwnerChanged));

    private ScrollViewer? _scrollViewer;

    public ScrollBarGutter()
    {
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public FrameworkElement? ScrollOwner
    {
        get => (FrameworkElement?)GetValue(ScrollOwnerProperty);
        set => SetValue(ScrollOwnerProperty, value);
    }

    private static void OnScrollOwnerChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var gutter = (ScrollBarGutter)dependencyObject;
        gutter._scrollViewer = null;
        gutter.UpdateWidth();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        LayoutUpdated += OnLayoutUpdated;
        UpdateWidth();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        LayoutUpdated -= OnLayoutUpdated;
        _scrollViewer = null;
    }

    private void OnLayoutUpdated(object? sender, EventArgs args) => UpdateWidth();

    private void UpdateWidth()
    {
        _scrollViewer ??= ScrollOwner as ScrollViewer ?? FindScrollViewer(ScrollOwner);
        var width = GetVisibleScrollBarWidth(_scrollViewer);

        if (!Width.Equals(width))
        {
            Width = width;
        }
    }

    private static double GetVisibleScrollBarWidth(ScrollViewer? scrollViewer)
    {
        if (scrollViewer?.ComputedVerticalScrollBarVisibility != Visibility.Visible)
        {
            return 0d;
        }

        var scrollBar = scrollViewer.Template.FindName("PART_VerticalScrollBar", scrollViewer)
            as System.Windows.Controls.Primitives.ScrollBar;
        return scrollBar is not null && scrollBar.ActualWidth > 0d
            ? scrollBar.ActualWidth
            : SystemParameters.VerticalScrollBarWidth;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject? parent)
    {
        if (parent is null)
        {
            return null;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            var descendant = FindScrollViewer(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
