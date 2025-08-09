namespace ClipTypr.Common;

using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;

public static partial class Util
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const int StackSizeBytes = 32_768;

    [GeneratedRegex(@"(\\Users\\)[^\\]+(?=\\|$)", RegexOptions.IgnoreCase)]
    private static partial Regex RedactUserRegex();

    public static string RedactUsername(string path) => RedactUserRegex().Replace(path, @"\Users\<REDACTED>");

    public static string GetFileNameTimestamp() => $"{DateTime.UtcNow:yyyyMMddHHmmssff}Z";

    public static string? FormatTime(in TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero) return null;

        var (value, unit) = timeSpan switch
        {
            var _ when timeSpan.TotalDays >= 1 => (timeSpan.TotalDays, "day"),
            var _ when timeSpan.TotalHours >= 1 => (timeSpan.TotalHours, "hour"),
            var _ when timeSpan.TotalMinutes >= 1 => (timeSpan.TotalMinutes, "minute"),
            var _ when timeSpan.TotalSeconds >= 1 => (timeSpan.TotalSeconds, "second"),
            var _ when timeSpan.TotalMilliseconds >= 1 => (timeSpan.TotalMilliseconds, "millisecond"),
            var _ when timeSpan.TotalMicroseconds >= 1 => (timeSpan.TotalMicroseconds, "microsecond"),
            _ => (timeSpan.TotalNanoseconds, "nanosecond")
        };

        if (value != 1) unit += 's';

        return $"{value:0.##} {unit}";
    }

    public static bool IsInStartup(string name, string path)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey))
        {
            if (key is null) return false;

            var value = key.GetValue(name)?.ToString();
            return value is not null && path.Equals(value.Trim('\"'), StringComparison.OrdinalIgnoreCase);
        }
    }

    public static bool AddToStartup(string name, string path)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
        {
            if (key is null) return false;

            key.SetValue(name, $"\"{path}\"");
            return true;
        }
    }

    public static bool RemoveFromStartup(string name)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
        {
            if (key is null) return false;

            key.DeleteValue(name);
            return true;
        }
    }

    public static bool IsRunAsAdmin()
    {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsSupportedConsole() => PInvoke.GetWindowLong(PInvoke.GetConsoleWindow(), -16) > 0;

    public static bool StartInSupportedConsole(bool runAsAdmin = false)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "conhost.exe"),
                Arguments = Environment.ProcessPath ?? throw new FileNotFoundException("The .exe path of the process couldn't be found"),
                UseShellExecute = true,
                Verb = runAsAdmin ? "runas" : ""
            });

            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public static string? OpenFileDialog(string title, string initialDirectory, string filter)
    {
        var ofn = new OPENFILENAME
        {
            lStructSize = Marshal.SizeOf<OPENFILENAME>(),
            lpstrFilter = filter,
            lpstrCustomFilter = null!,
            nMaxCustFilter = 0,
            nFilterIndex = 1,
            lpstrFile = new string(stackalloc char[256]),
            nMaxFile = 256,
            lpstrFileTitle = null!,
            nMaxFileTitle = 0,
            lpstrInitialDir = initialDirectory,
            lpstrTitle = title,
            Flags = 0x00080000 | 0x00001000,
            nFileOffset = 0,
            nFileExtension = 0,
            lpstrDefExt = null!
        };

        return PInvoke.GetOpenFileName(ref ofn) ? ofn.lpstrFile : null;
    }
}