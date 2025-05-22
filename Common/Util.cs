namespace ClipTypr.Common;

using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.RegularExpressions;

public static class Util
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const int StackSizeBytes = 1024;

    public static unsafe bool AllowStack<T>(int size) where T : unmanaged
        => sizeof(T) * size <= StackSizeBytes;

    public static string? FormatTime(in TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero) return null;

        var (value, unit) = timeSpan switch
        {
            var _ when timeSpan.TotalDays >= 1 => (timeSpan.TotalDays, "day"),
            var _ when timeSpan.TotalHours >= 1 => (timeSpan.TotalHours, "hour"),
            var _ when timeSpan.TotalMinutes >= 1 => (timeSpan.TotalMinutes, "minute"),
            var _ when timeSpan.TotalSeconds >= 1 => (timeSpan.TotalSeconds, "second"),
            var _ when timeSpan.TotalMilliseconds >= 1 => (timeSpan.TotalMilliseconds, "milliseconds"),
            var _ when timeSpan.TotalMicroseconds >= 1 => (timeSpan.TotalMicroseconds, "microseconds"),
            _ => (timeSpan.TotalNanoseconds, "nanoseconds")
        };

        if (value != 1) unit += 's';

        return $"{value:0.##} {unit}";
    }

    public static bool IsRunAsAdmin()
    {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsInStartup(string appName, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey);
        if (key is null)
        {
            Logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return false;
        }

        var value = key.GetValue(appName)?.ToString();
        if (value is null) return false;

        return executablePath.Equals(value.Trim('\"'), StringComparison.OrdinalIgnoreCase);
    }

    public static void AddToStartup(string appName, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key is null)
        {
            Logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return;
        }

        key.SetValue(appName, $"\"{executablePath}\"");
    }

    public static void RemoveFromStartup(string appName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key is null)
        {
            Logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return;
        }

        key.DeleteValue(appName);
    }

    public static string RedactUsername(string path)
    {
        var username = Path.GetFileName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        return Regex.Replace(
            path,
            $@"(\\Users\\){Regex.Escape(username)}(?=\\|$)",
            @"\Users\<REDACTED>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public static void OpenGitHubIssue(string version, string message, string stackTrace)
    {
        Clipboard.SetText($"```cs\n{RedactUsername(stackTrace)}\n```");

        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = $"https://github.com/BlyZeDev/{nameof(ClipTypr)}/issues/new?template=issue.yaml&title={message}&version={version}",
                UseShellExecute = true
            };
            process.Start();
        }
    }
}