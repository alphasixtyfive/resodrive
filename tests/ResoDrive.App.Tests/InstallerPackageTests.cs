using System.Xml.Linq;

namespace ResoDrive.App.Tests;

public sealed class InstallerPackageTests
{
    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";

    [Fact]
    public void UpgradePreparationIsBestEffortAndRunsWithoutPowerShell()
    {
        var action = LoadAction("PrepareInstalledResoDriveForUpgrade");

        Assert.Equal("ResoDriveExecutableFile", (string?)action.Attribute("FileRef"));
        Assert.Equal("--prepare-update", (string?)action.Attribute("ExeCommand"));
        Assert.Equal("ignore", (string?)action.Attribute("Return"));
        Assert.Equal("yes", (string?)action.Attribute("Impersonate"));
    }

    [Fact]
    public void UpgradeStopFallbackIsHiddenPathScopedAndWaitsForExit()
    {
        var action = LoadAction("StopResoDriveForUpgrade");
        var command = Assert.IsType<XAttribute>(action.Attribute("ExeCommand")).Value;

        Assert.Equal("check", (string?)action.Attribute("Return"));
        Assert.Equal("no", (string?)action.Attribute("Impersonate"));
        Assert.Contains("-WindowStyle Hidden", command, StringComparison.Ordinal);
        Assert.Contains("Path -eq $p", command, StringComparison.Ordinal);
        Assert.Contains("WaitForExit()", command, StringComparison.Ordinal);
        Assert.Contains(";exit 0", command, StringComparison.Ordinal);
    }

    private static XElement LoadAction(string id)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Package.wxs"));
        return Assert.Single(document.Descendants(Wix + "CustomAction"), action =>
            (string?)action.Attribute("Id") == id);
    }
}
