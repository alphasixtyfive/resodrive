using Microsoft.Win32;
using ResoDrive.Core.Results;

namespace ResoDrive.Windows;

public sealed class RegistryAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "rdrive";
    private readonly string _applicationPath;

    public RegistryAutostartService(string applicationPath) =>
        _applicationPath = Path.GetFullPath(applicationPath);

    public Task<OperationResult<bool>> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return Task.FromResult(Result.Success(IsOwnedCommand(value)));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Task.FromResult(Result.Failure<bool>("autostart.access_denied", exception.Message));
        }
    }

    public Task<OperationResult> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                key.SetValue(ValueName, Command, RegistryValueKind.String);
            }
            else
            {
                var existing = key.GetValue(ValueName) as string;
                if (existing is not null && !IsOwnedCommand(existing))
                {
                    return Task.FromResult(Result.Failure(
                        "autostart.foreign_value",
                        "The ResoDrive startup entry belongs to a different installation and was left unchanged."));
                }

                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return Task.FromResult(Result.Success());
        }
        catch (UnauthorizedAccessException exception)
        {
            return Task.FromResult(Result.Failure("autostart.access_denied", exception.Message));
        }
    }

    private bool IsOwnedCommand(string? value) =>
        string.Equals(value, Command, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, LegacyCommand, StringComparison.OrdinalIgnoreCase);

    private string Command => AutostartCommand.Create(_applicationPath);

    // Accepted only so an existing ResoDrive entry can be upgraded or removed safely.
    private string LegacyCommand => $"\"{_applicationPath}\"";
}

public static class AutostartCommand
{
    public const string BackgroundArgument = "--background";

    public static string Create(string applicationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationPath);
        return $"\"{Path.GetFullPath(applicationPath)}\" {BackgroundArgument}";
    }

    public static bool IsBackgroundArgument(string? argument) =>
        string.Equals(argument, BackgroundArgument, StringComparison.OrdinalIgnoreCase);
}
