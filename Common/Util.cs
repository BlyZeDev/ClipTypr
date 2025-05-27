namespace ClipTypr.Common;

using System.Security.Principal;
using System.Text.RegularExpressions;

public static class Util
{
    public const int StackSizeBytes = 32_768;

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

    public static string RedactUsername(string path)
    {
        var username = Path.GetFileName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        return Regex.Replace(
            path,
            $@"(\\Users\\){Regex.Escape(username)}(?=\\|$)",
            @"\Users\<REDACTED>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}