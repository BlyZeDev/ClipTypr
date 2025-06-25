namespace ClipTypr.Common;

using System.Diagnostics;

public sealed class Plugin
{
    private readonly string _pluginPath;

    public Plugin(string filepath) => _pluginPath = filepath;

    public PluginResult Execute(string filepath)
    {
        if (!File.Exists(_pluginPath))
        {
            return new PluginResult
            {
                ExitCode = 0,
                ErrorMessage = "The original file does not exist",
                FilePath = null
            };
        }

        if (!Path.GetExtension(_pluginPath).Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            return new PluginResult
            {
                ExitCode = 0,
                ErrorMessage = "The script is not a valid powershell (.ps1)",
                FilePath = null
            };
        }

        string? result = null;
        string? error = null;
        using (var process = new Process())
        {
            process.EnableRaisingEvents = true;
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{_pluginPath}\" \"{filepath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data)) result = args.Data;
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data)) error = args.Data;
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit(TimeSpan.FromSeconds(10));

            return new PluginResult
            {
                ExitCode = process.ExitCode,
                ErrorMessage = error ?? "Something went wrong within the script",
                FilePath = result
            };
        }
    }

    public override string ToString() => _pluginPath;
}