namespace StrokeMyKeys;

using System.IO;
using System.Text;
using System.Text.Json;

public sealed class ConfigurationHandler : IDisposable
{
    private const string ConfigName = "appdata.json";

    private static readonly Config _defaultConfig = new Config
    {
        IsFirstStart = true,
        PasteCooldownMs = 3000
    };
    private static readonly Config _errorConfig = new Config
    {
        IsFirstStart = false,
        PasteCooldownMs = 3000
    };

    private readonly string? _configPath;
    private readonly FileSystemWatcher? _watcher;

    public bool HasAccess => _configPath is not null;

    public Config Current { get; private set; }

    public ConfigurationHandler()
    {
        var directory = AppContext.BaseDirectory;

        if (!IsPathUsable(Path.Combine(directory, ConfigName)))
            directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var hasAccess = !IsPathUsable(Path.Combine(directory, ConfigName));

        _configPath = hasAccess ? Path.Combine(directory, ConfigName) : null;
        _watcher = hasAccess ? new FileSystemWatcher
        {
            EnableRaisingEvents = true,
            Path = directory,
            Filter = ConfigName,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite
        } : null;
        if (_watcher is not null) _watcher.Changed += OnConfigFileChange;

        Current = hasAccess ? _errorConfig : _defaultConfig;

        Read(_configPath);
    }

    private void OnConfigFileChange(object sender, FileSystemEventArgs e)
    {
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

        using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            using (var reader = new StreamReader(fileStream, Encoding.UTF8, true, -1, true))
            {
                var json = reader.ReadToEnd();
                Current = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.Config) ?? _errorConfig;
            }
        }
    }

    private static void Write(string? path, Config config)
    {
        if (path is null) return;

        using (var fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write))
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

    private static bool IsPathUsable(string path)
    {
        try
        {
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
}