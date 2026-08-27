using System.ComponentModel;
using Microsoft.Win32;
using ResoDrive.Core.Contracts;
using ResoDrive.Core.Results;

namespace ResoDrive.Windows;

public static class WinFspPrerequisiteService
{
    public static readonly Uri OfficialReleasesUri = new("https://github.com/winfsp/winfsp/releases");

    public static async Task<OperationResult<PrerequisiteStatus>> InspectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The registry API has no asynchronous equivalent and can occasionally
        // block while Windows Installer data is being serviced. Keep that work
        // away from the WPF dispatcher.
        var version = await Task.Run(FindInstalledVersion, cancellationToken).ConfigureAwait(false);
        if (version is not null)
        {
            return Result.Success(new PrerequisiteStatus(true, version));
        }

        try
        {
            var service = await ProcessRunner.RunAsync(
                Path.Combine(Environment.SystemDirectory, "sc.exe"),
                ["query", "WinFsp.Launcher"],
                TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
            if (!service.TimedOut && service.ExitCode == 0)
            {
                return Result.Success(new PrerequisiteStatus(true, null));
            }
        }
        catch (Exception exception)
            when (exception is IOException or InvalidOperationException or Win32Exception)
        {
            // Registry detection is authoritative for the version. A failed service fallback
            // simply means setup should present WinFsp as unavailable.
        }

        return Result.Success(new PrerequisiteStatus(false, null));
    }

    private static string? FindInstalledVersion()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var uninstall = machine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    writable: false);
                if (uninstall is null)
                {
                    continue;
                }

                foreach (var subkeyName in uninstall.GetSubKeyNames())
                {
                    using var product = uninstall.OpenSubKey(subkeyName, writable: false);
                    var displayName = product?.GetValue("DisplayName") as string;
                    if (displayName?.StartsWith("WinFsp", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        continue;
                    }

                    return (product?.GetValue("DisplayVersion") as string)?.Trim() ?? string.Empty;
                }
            }
            catch (Exception exception)
                when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                // Continue with the other registry view and then the service fallback.
            }
        }

        return null;
    }
}
