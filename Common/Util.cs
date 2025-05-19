namespace ClipTypr.Common;

using System.Diagnostics;
using System.Text.RegularExpressions;

public static class Util
{
    public const int StackSizeBytes = 1024;

    public static unsafe bool AllowStack<T>(int size) where T : unmanaged
        => sizeof(T) * size <= StackSizeBytes;

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