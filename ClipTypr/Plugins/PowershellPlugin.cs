namespace ClipTypr.Plugins;

using System.Diagnostics;

public sealed class PowershellPlugin : IPlugin
{
    public const string FileExtension = ".ps1";

    public string ScriptPath { get; }

    public PowershellPlugin(string scriptPath) => ScriptPath = scriptPath;

    public PluginResult Execute(string filepath)
    {
        if (!File.Exists(ScriptPath))
        {
            return new PluginResult
            {
                Error = new ScriptException("The original file does not exist"),
                FilePath = null
            };
        }

        if (!Path.GetExtension(ScriptPath).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            return new PluginResult
            {
                Error = new ScriptException($"The script is not a valid powershell ({FileExtension})"),
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
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{ScriptPath}\" \"{filepath}\"",
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
                Error = new ScriptException(error ?? "Something went wrong within the script"),
                FilePath = result
            };
        }
    }

    public override string ToString() => ScriptPath;
}