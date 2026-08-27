using System.Xml.Linq;

namespace ResoDrive.App.Tests;

public sealed class MainWindowMarkupTests
{
    [Fact]
    public void SyncRowsUseSingleBaselineStatusFooterWithoutProgressBars()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var bindings = document
            .Descendants()
            .Attributes("Text")
            .Select(attribute => attribute.Value)
            .ToArray();

        var syncRowTemplate = Assert.Single(
            document.Descendants(presentation + "DataTemplate"),
            template => template.Descendants().Attributes("Text")
                .Any(attribute => attribute.Value == "{Binding StatusLine}"));
        Assert.Empty(syncRowTemplate.Descendants(presentation + "ProgressBar"));
        Assert.Contains("{Binding StatusLine}", bindings);
        Assert.DoesNotContain("{Binding StatusPrimary}", bindings);
        Assert.DoesNotContain("{Binding StatusSecondary}", bindings);
    }

    [Fact]
    public void StatusRailRadiusFitsItsThreePixelWidth()
    {
        var document = Load("MainWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var style = Assert.Single(
            document.Descendants(presentation + "Style"),
            element => (string?)element.Attribute(xaml + "Key") == "RowStatusRail");
        var radius = Assert.Single(
            style.Elements(presentation + "Setter"),
            element => (string?)element.Attribute("Property") == "CornerRadius")
            .Attribute("Value")?.Value;

        Assert.NotNull(radius);
        Assert.True(double.Parse(radius.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture) <= 1.5);
    }

    [Fact]
    public void FieldTemplatesApplyTheirSemanticPadding()
    {
        var document = Load("Controls.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var contentHosts = document.Descendants(presentation + "ScrollViewer")
            .Where(element => (string?)element.Attribute(xaml + "Name") == "PART_ContentHost")
            .ToArray();

        Assert.Equal(2, contentHosts.Count(host =>
            (string?)host.Attribute("Padding") == "{TemplateBinding Padding}"));
    }

    [Fact]
    public void WelcomeIsCompactAndHasThreePagesWithoutScrollbars()
    {
        var document = Load("WelcomeWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.Empty(document.Descendants(presentation + "ScrollViewer"));
        foreach (var name in new[] { "DrivePage", "SyncPage", "RclonePage" })
        {
            Assert.Single(document.Descendants(), element =>
                (string?)element.Attribute(xaml + "Name") == name);
        }
    }

    [Fact]
    public void SetupUsesOneResponsiveScrollSurfaceAndSharedArgumentEditor()
    {
        var document = Load("SetupWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.Single(document.Descendants(presentation + "ScrollViewer"));
        var arguments = Assert.Single(document.Descendants(presentation + "TextBox"), element =>
            (string?)element.Attribute(xaml + "Name") == "ArgumentsBox");
        Assert.Equal("{StaticResource ArgumentTextBox}", (string?)arguments.Attribute("Style"));
        Assert.Single(document.Descendants(), element =>
            (string?)element.Attribute(xaml + "Name") == "ProfilePanel");
    }

    [Fact]
    public void SetupDoesNotPrefillRequiredUserTextFields()
    {
        var document = Load("SetupWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (var name in new[] { "UsernameBox", "DisplayNameBox" })
        {
            var field = Assert.Single(document.Descendants(presentation + "TextBox"), element =>
                (string?)element.Attribute(xaml + "Name") == name);
            Assert.True(string.IsNullOrEmpty((string?)field.Attribute("Text")));
        }

        var port = Assert.Single(document.Descendants(presentation + "TextBox"), element =>
            (string?)element.Attribute(xaml + "Name") == "PortBox");
        Assert.Equal("22", (string?)port.Attribute("Text"));
    }

    [Fact]
    public void SettingsExposeSeparateApplicationAndRcloneUpdateActions()
    {
        var document = Load("MainWindow.xaml");
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (var name in new[]
                 {
                     "CheckApplicationUpdateButton",
                     "GetApplicationUpdateButton",
                     "CheckRcloneUpdateButton",
                     "UpdateRcloneButton"
                 })
        {
            Assert.Single(document.Descendants(), element =>
                (string?)element.Attribute(xaml + "Name") == name);
        }
    }

    [Fact]
    public void AboutWindowExposesAccessibleRepositoryLinks()
    {
        var document = Load("AboutWindow.xaml");
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var actions = document.Descendants(presentation + "Button")
            .Select(button => (string?)button.Attribute("Click"))
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains("OpenRepository_Click", actions);
        Assert.Contains("OpenIssues_Click", actions);
    }

    private static XDocument Load(string name) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
