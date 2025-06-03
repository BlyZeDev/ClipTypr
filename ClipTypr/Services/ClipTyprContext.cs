namespace ClipTypr.Services;

using Microsoft.Win32;
using System.Drawing;
using System.Text;

public sealed class ClipTyprContext
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string Version = "2.1.0";

    private readonly ILogger _logger;

    /// <summary>
    /// The base directory of the application
    /// </summary>
    public string ApplicationDirectory { get; }

    /// <summary>
    /// The full path to the .exe of this application
    /// </summary>
    public string ExecutablePath { get; }

    /// <summary>
    /// The base directory to store application files
    /// </summary>
    public string AppFilesDirectory { get; }

    /// <summary>
    /// The path to application icon
    /// </summary>
    public string IcoPath { get; }

    public ClipTyprContext(ILogger logger)
    {
        _logger = logger;

        ApplicationDirectory = AppContext.BaseDirectory;
        _logger.LogDebug($"{nameof(ApplicationDirectory)}: {ApplicationDirectory}");

        ExecutablePath = Environment.ProcessPath ?? throw new ApplicationException("The path of the executable could not be found");
        _logger.LogDebug($"{nameof(ExecutablePath)}: {ExecutablePath}");

        AppFilesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(ClipTypr));
        Directory.CreateDirectory(AppFilesDirectory);
        _logger.LogDebug($"{nameof(AppFilesDirectory)}: {AppFilesDirectory}");

        var icoPath = Path.Combine(ApplicationDirectory, "icon.ico");
        if (!File.Exists(icoPath)) icoPath = GetFallbackIco();
        if (icoPath is null) throw new MissingIconException("No icon could be found");

        IcoPath = icoPath;
        _logger.LogDebug($"{nameof(IcoPath)}: {IcoPath}");
    }

    public bool IsInStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey);
        if (key is null)
        {
            _logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return false;
        }

        var value = key.GetValue(nameof(ClipTypr))?.ToString();
        return value is not null && ExecutablePath.Equals(value.Trim('\"'), StringComparison.OrdinalIgnoreCase);
    }

    public void AddToStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key is null)
        {
            _logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return;
        }

        key.SetValue(nameof(ClipTypr), $"\"{ExecutablePath}\"");
    }

    public void RemoveFromStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key is null)
        {
            _logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return;
        }

        key.DeleteValue(nameof(ClipTypr));
    }

    public string WriteCrashLog(Exception exception)
    {
        var crashLogPath = Path.Combine(ApplicationDirectory, $"{nameof(ClipTypr)}-Crash-{DateTime.UtcNow:yyyyMMddHHmmssff}Z.log");

        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Options = FileOptions.WriteThrough,
            Share = FileShare.None
        };
        using (var writer = new StreamWriter(crashLogPath, Encoding.UTF8, options))
        {
            writer.Write(exception.ToString());
        }

        return crashLogPath;
    }

    private static string? GetFallbackIco()
    {
        const int ControlPanelIcon = 43;

        var hIcon = Native.ExtractIcon(nint.Zero, Path.Combine(Environment.SystemDirectory, "shell32.dll"), ControlPanelIcon);
        if (hIcon == nint.Zero) return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-Fallback.ico");

        using (var icon = Icon.FromHandle(hIcon))
        {
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                icon.Save(fileStream);
                fileStream.Flush();
            }
        }

        return tempPath;
    }
}