using System.IO;
using Microsoft.Win32;

namespace BinLet;

/// <summary>
/// Per-user "start with Windows" via HKCU\...\Run (no admin rights needed).
/// The registry is the single source of truth; nothing is cached or stored elsewhere.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Task Manager / Settings record a user's enable/disable choice here, per value name.
    private const string ApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "Binlet";

    private static string Command =>
        $"\"{Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path.")}\"";

    /// <summary>
    /// True only if our Run entry points at this executable and the user has not
    /// disabled it in Task Manager / Settings.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (run?.GetValue(ValueName) is not string current ||
                !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
                return false;

            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath);
            // First byte: even = enabled (02/06), odd = disabled (03/07).
            return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } data || (data[0] & 1) == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>Adds or removes the Run entry. Throws if the registry cannot be written.</summary>
    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var run = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            run.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            run?.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        // Clear any stale approval state for our own value so the new state takes effect.
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKeyPath, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
