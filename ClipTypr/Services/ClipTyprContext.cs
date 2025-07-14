namespace ClipTypr.Services;

using Microsoft.Win32;
using System.Drawing;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

public sealed partial class ClipTyprContext : IDisposable
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string Version = "2.3.0";
    public const string ConfigFileName = "usersettings.json";

    [GeneratedRegex(@"(\\Users\\)[^\\]+(?=\\|$)", RegexOptions.IgnoreCase)]
    private static partial Regex RedactUserRegex();

    private readonly ILogger _logger;
    private readonly HashSet<string> _tempPaths;

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

    /// <summary>
    /// The path to the configuration
    /// </summary>
    public string ConfigurationPath { get; }

    /// <summary>
    /// The base directory for all plugins
    /// </summary>
    public string PluginDirectory { get; }

    public ClipTyprContext(ILogger logger)
    {
        _logger = logger;

        _tempPaths = [];

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

        ConfigurationPath = Path.Combine(AppFilesDirectory, ConfigFileName);
        _logger.LogDebug($"{nameof(ConfigurationPath)}: {ConfigurationPath}");

        PluginDirectory = Path.Combine(AppFilesDirectory, "Plugins");
        Directory.CreateDirectory(PluginDirectory);
        _logger.LogDebug($"{nameof(PluginDirectory)}: {PluginDirectory}");
    }

    public string GetTempPath(string fileExtension)
    {
        string tempPath;
        do
        {
            tempPath = Path.ChangeExtension(Path.Combine(Path.GetTempPath(), Guid.CreateVersion7().ToString("N")), fileExtension);
        } while (!_tempPaths.Add(tempPath));

        return tempPath;
    }

    public bool IsRunAsAdmin()
    {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public string RedactUsername(string path) => RedactUserRegex().Replace(path, @"\Users\<REDACTED>");

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

    public void Dispose()
    {
        var cleanedFileCount = 0;

        foreach (var tempPath in _tempPaths)
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                cleanedFileCount++;
            }
        }

        if (cleanedFileCount > 0) _logger.LogInfo($"Cleaned up {cleanedFileCount} files");

        GC.SuppressFinalize(this);
    }

    private static string? GetFallbackIco()
    {
        const int FallbackIconIndex = 0;

        var tempPath = Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-Fallback.ico");

        var iconHandle = Native.ExtractIcon(nint.Zero, Path.Combine(Environment.SystemDirectory, "imageres.dll"), FallbackIconIndex);
        using (var icon = iconHandle == nint.Zero ? SystemIcons.GetStockIcon(StockIconId.Error, StockIconOptions.SmallIcon) : Icon.FromHandle(iconHandle))
        {
            if (icon is null) return null;

            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                icon.Save(fileStream);
                fileStream.Flush();
            }
        }

        return tempPath;
    }
}