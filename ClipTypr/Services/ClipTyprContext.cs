namespace ClipTypr.Services;

using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
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
    /// The handle to the to application icon
    /// </summary>
    public nint IcoHandle { get; }

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

        var icoPath = CreateMainIco();
        if (!File.Exists(icoPath)) icoPath = CreateFallbackIco();
        if (!File.Exists(icoPath)) throw new MissingIconException("No icon could be created");

        IcoHandle = GetIcoHandle(icoPath);
        if (IcoHandle == nint.Zero) throw new MissingIconException("No icon could be found");

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

    private unsafe string? CreateMainIco()
    {
        var handle = Process.GetCurrentProcess().Handle;
        if (handle == nint.Zero) return null;

        var resourceInfoHandle = Native.FindResource(handle, "ICON", "RCDATA");
        if (resourceInfoHandle == nint.Zero) return null;

        var resourceDataHandle = Native.LoadResource(handle, resourceInfoHandle);
        if (resourceDataHandle == nint.Zero) return null;

        var resourceHandle = Native.LockResource(resourceDataHandle);
        if (resourceHandle == nint.Zero) return null;

        var size = Native.SizeofResource(handle, resourceInfoHandle);
        var buffer = new ReadOnlySpan<byte>((void*)resourceHandle, (int)size);

        var tempPath = GetTempPath(".ico");
        File.WriteAllBytes(tempPath, buffer);

        return tempPath;
    }

    private string? CreateFallbackIco()
    {
        const int FallbackIconIndex = 0;

        var tempPath = GetTempPath(".ico");

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

    private static unsafe nint GetIcoHandle(string icoPath)
    {
        var smallIcon = stackalloc nint[1];
        var largeIcon = stackalloc nint[1];

        _ = Native.ExtractIconEx(icoPath, 0, largeIcon, smallIcon, 1);

        var icoHandle = largeIcon[0];
        return icoHandle == nint.Zero ? smallIcon[0] : icoHandle;
    }
}