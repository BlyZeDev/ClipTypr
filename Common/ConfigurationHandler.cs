namespace StrokeMyKeys.Common;

using System.IO;
using System.Text;
using System.Text.Json;

public sealed class ConfigurationHandler : IDisposable
{
    private const string ConfigName = "usersettings.json";

    private static readonly Config _defaultConfig = new Config
    {
        PasteCooldownMs = 3000,
        LogLevel = LogLevel.Info
    };

    private readonly FileSystemWatcher _watcher;
    private readonly Action<Config> _onConfigReload;

    public string ConfigPath { get; }

    public Config Current { get; private set; }

    public ConfigurationHandler(Action<Config> onConfigReload)
    {
        _onConfigReload = onConfigReload;

        var directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        ConfigPath = Path.Combine(directory, ConfigName);

        Logger.LogDebug($"Configuration Path: {ConfigPath}");

        _watcher = new FileSystemWatcher
        {
            Path = directory,
            Filter = ConfigName,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnConfigFileChange;
        _watcher.Created += OnConfigFileChange;
        _watcher.Deleted += OnConfigFileChange;
        _watcher.Renamed += OnConfigFileRenamed;
        _watcher.Error += OnWatcherError;

        Current = _defaultConfig;

        if (!File.Exists(ConfigPath)) Write(_defaultConfig);
        Reload();
    }

    public void Write(Config config)
    {
        try
        {
            using (var fileStream = new FileStream(ConfigPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8, -1, true))
                {
                    var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.Config);

                    writer.Write(json);
                    writer.Flush();
                }

                fileStream.Flush();
            }

            Logger.LogDebug("Configuration was overwritten");
        }
        catch (IOException ex) when (IsFileLocked(ex))
        {
            Logger.LogDebug("The file is currently locked, ignoring.");
        }
    }

    public void Dispose()
    {
        _watcher.Changed -= OnConfigFileChange;
        _watcher.Created -= OnConfigFileChange;
        _watcher.Deleted -= OnConfigFileChange;
        _watcher.Renamed -= OnConfigFileRenamed;
        _watcher.Error -= OnWatcherError;
        _watcher.Dispose();
    }

    private void Reload()
    {
        try
        {
            using (var fileStream = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = new StreamReader(fileStream, Encoding.UTF8, true, -1, true))
                {
                    var json = reader.ReadToEnd();
                    Current = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config) ?? throw new JsonException("The configuration cannot be null");
                }
            }

            _onConfigReload(Current);

            Logger.LogInfo("Reloaded the configuration");
        }
        catch (IOException ex) when (IsFileLocked(ex))
        {
            Logger.LogDebug("The file is currently locked, ignoring.");
        }
        catch (JsonException ex)
        {
            Logger.LogWarning($"The configuration contains an error on line {(ex.LineNumber ?? -2) + 1}. Resetting to the last correct configuration");
            Write(Current);
        }
    }

    private void OnConfigFileChange(object sender, FileSystemEventArgs e)
    {
        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Created or WatcherChangeTypes.Changed:
                Logger.LogDebug($"Configuration - {e.ChangeType}");
                Reload();
                break;

            case WatcherChangeTypes.Deleted:
                Logger.LogDebug($"Configuration - {e.ChangeType}");
                Write(_defaultConfig);
                break;
        }
    }

    private void OnConfigFileRenamed(object sender, RenamedEventArgs e)
    {
        Logger.LogDebug($"Configuration - {e.ChangeType}");

        if (ConfigPath == e.FullPath) Reload();
        else Write(_defaultConfig);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e) => Logger.LogError("The configuration file can't be monitored", e.GetException());

    private static bool IsFileLocked(IOException ex)
    {
        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;

        return (ex.HResult & 0xFFFF) is ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION;
    }
}