namespace ClipTypr.Services;

using System.Drawing;
using System.Reflection;
using System.Text;

public sealed class ClipTyprContext : IDisposable
{
    public const string Version = "2.4.1";
    public const string ConfigFileName = "usersettings.json";

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
    /// The base directory for all log files
    /// </summary>
    public string LogDirectory { get; }

    public ClipTyprContext()
    {
        _tempPaths = [];

        ApplicationDirectory = AppContext.BaseDirectory;

        ExecutablePath = Environment.ProcessPath ?? throw new ApplicationException("The path of the executable could not be found");

        AppFilesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(ClipTypr));
        Directory.CreateDirectory(AppFilesDirectory);

        var icoPath = CreateMainIco();
        if (!File.Exists(icoPath)) icoPath = CreateFallbackIco();
        if (!File.Exists(icoPath)) throw new MissingIconException("No icon could be created");

        IcoHandle = GetIcoHandle(icoPath);
        if (IcoHandle == nint.Zero) throw new MissingIconException("No icon could be found");

        ConfigurationPath = Path.Combine(AppFilesDirectory, ConfigFileName);

        LogDirectory = Path.Combine(AppFilesDirectory, "Logs");
        Directory.CreateDirectory(LogDirectory);
    }

    public string GetTempPath(string fileExtension)
    {
        string tempPath;
        do
        {
            tempPath = Path.ChangeExtension(Path.Combine(nameof(ClipTypr), Path.GetTempPath(), Guid.CreateVersion7().ToString("N")), fileExtension);
        } while (!_tempPaths.Add(tempPath));

        return tempPath;
    }

    public string WriteCrashLog(Exception exception)
    {
        var crashLogPath = Path.Combine(LogDirectory, $"{nameof(ClipTypr)}-Crash-{Util.GetFileNameTimestamp()}.log");

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
        foreach (var tempPath in _tempPaths)
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception) { }
            }
        }

        var deletetionBaseline = DateTime.UtcNow.AddDays(-7);
        foreach (var logFile in Directory.EnumerateFiles(LogDirectory, "*.log", SearchOption.TopDirectoryOnly))
        {
            if (File.GetCreationTimeUtc(logFile) < deletetionBaseline)
            {
                try
                {
                    File.Delete(logFile);
                }
                catch (Exception) { }
            }
        }

        GC.SuppressFinalize(this);
    }

    private unsafe string? CreateMainIco()
    {
        using (var icoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{nameof(ClipTypr)}.icon.ico"))
        {
            if (icoStream is null) return null;

            var tempPath = GetTempPath(".ico");

            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                icoStream.CopyTo(fileStream);
                fileStream.Flush();
            }

            return tempPath;
        }
    }

    private string? CreateFallbackIco()
    {
        const int FallbackIconIndex = 0;

        var tempPath = GetTempPath(".ico");

        var iconHandle = PInvoke.ExtractIcon(nint.Zero, Path.Combine(Environment.SystemDirectory, "imageres.dll"), FallbackIconIndex);
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

        _ = PInvoke.ExtractIconEx(icoPath, 0, largeIcon, smallIcon, 1);

        var icoHandle = largeIcon[0];
        return icoHandle == nint.Zero ? smallIcon[0] : icoHandle;
    }
}