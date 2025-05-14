namespace StrokeMyKeys;

using System.IO;
using System.Text;
using System.Text.Json;

public sealed class ConfigurationHandler : IDisposable
{
    private const string ConfigName = "appdata.json";

    private static readonly Config _defaultConfig = new Config
    {
        PasteCooldownMs = 3000
    };

    private readonly string? _configPath;
    private readonly FileSystemWatcher? _watcher;

    public bool HasAccess => _configPath is not null;

    public string ConfigPath => _configPath ?? "";

    public Config Current { get; private set; }

    public ConfigurationHandler()
    {
        var directory = AppContext.BaseDirectory;

        if (!IsDirectoryUsable(directory))
            directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var hasAccess = IsDirectoryUsable(directory);

        _configPath = hasAccess ? Path.Combine(directory, ConfigName) : null;
        _watcher = hasAccess ? new FileSystemWatcher
        {
            Path = directory,
            Filter = ConfigName,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        } : null;
        if (_watcher is not null) _watcher.Changed += OnConfigFileChange;

        Current = _defaultConfig;

        if (hasAccess && !File.Exists(_configPath)) Write(_defaultConfig);
        Read(_configPath);
    }

    private void OnConfigFileChange(object sender, FileSystemEventArgs e)
    {
        Logger.LogInfo($"Configuration - {e.ChangeType}");

        switch (e.ChangeType)
        {
            case WatcherChangeTypes.Created or WatcherChangeTypes.Changed:
                Read(e.FullPath);
                break;

            case WatcherChangeTypes.Deleted:
                Write(e.FullPath, _defaultConfig);
                break;
        }
    }

    public void Write(Config config) => Write(_configPath, config);

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnConfigFileChange;
            _watcher.Dispose();
        }
    }

    private void Read(string? path)
    {
        if (path is null) return;

        var needsReset = false;
        using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (var reader = new StreamReader(fileStream, Encoding.UTF8, true, -1, true))
            {
                var json = reader.ReadToEnd();
                var config = TryDeserialize(json);
                needsReset = config is null;

                Current = config ?? _defaultConfig;
            }
        }

        if (needsReset) Write(path, _defaultConfig);
    }

    private static void Write(string? path, Config config)
    {
        if (path is null) return;

        using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            using (var writer = new StreamWriter(fileStream, Encoding.UTF8, -1, true))
            {
                var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.Config);

                writer.Write(json);
                writer.Flush();
            }

            fileStream.Flush();
        }
    }

    private static bool IsDirectoryUsable(string directory)
    {
        try
        {
            var path = Path.Combine(directory, Guid.CreateVersion7().ToString());

            using (var writer = File.CreateText(path))
            {
                writer.Write("---");
                writer.Flush();
            }

            File.Delete(path);

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("The path is not usable", ex);
        }

        return false;
    }

    private static Config? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config);
        }
        catch (Exception)
        {
            return null;
        }
    }
}