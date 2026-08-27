using System.Windows.Media;
using WpfPath = System.Windows.Shapes.Path;

namespace ResoDrive.App;

internal static class StatusVisuals
{
    private static readonly Geometry SuccessGeometry = Create("M 1,7 L 5,11 L 13,2");
    private static readonly Geometry WarningGeometry = Create("M 7,1 L 13,13 L 1,13 Z M 7,5 L 7,9 M 7,11 L 7,11.1");
    private static readonly Geometry PendingGeometry = Create("M 7,1 A 6,6 0 1 1 6.9,1 M 7,4 L 7,7 L 9,9");

    public static void Apply(WpfPath icon, bool success, bool error = false)
    {
        ArgumentNullException.ThrowIfNull(icon);
        icon.Data = success ? SuccessGeometry : WarningGeometry;
        icon.Stroke = success
            ? StatusPalette.Success
            : error
                ? StatusPalette.Error
                : StatusPalette.Warning;
    }

    public static void ApplyPending(WpfPath icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        icon.Data = PendingGeometry;
        icon.Stroke = StatusPalette.Muted;
    }

    private static Geometry Create(string data)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}
