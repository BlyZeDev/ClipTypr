namespace ClipTypr.Common;

using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.RegularExpressions;

public static partial class Util
{
    public const int StackSizeBytes = 32_768;

    [GeneratedRegex(@"(\\Users\\)[^\\]+(?=\\|$)", RegexOptions.IgnoreCase)]
    private static partial Regex RedactUserRegex();

    public static string RedactUsername(string path) => RedactUserRegex().Replace(path, @"\Users\<REDACTED>");

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

    public static bool IsRunAsAdmin()
    {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsSupportedConsole() => Native.GetWindowLong(Native.GetConsoleWindow(), -16) > 0;

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
}