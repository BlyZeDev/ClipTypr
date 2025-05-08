namespace StrokeMyKeys;

using System.IO;
using System.Text;

public sealed class ConfigurationHandler
{
    private static readonly Config _defaultConfig = new Config
    {
        IsFirstStart = true
    };
    private static readonly Config _errorConfig = new Config
    {
        IsFirstStart = false
    };

    private readonly string? _configPath;

    public Config Current { get; private set; }

    public ConfigurationHandler()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "appdata.config");

        if (!IsPathUsable(_configPath))
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "appdata.config");

        if (!IsPathUsable(_configPath))
        {
            _configPath = null;
            Current = _errorConfig;
        }
        else Current = _defaultConfig;
        
        Read();
    }

    public void Write(Config config)
    {
        if (_configPath is null) return;

        using (var fileStream = new FileStream(_configPath, FileMode.OpenOrCreate, FileAccess.Write))
        {
            using (var writer = new BinaryWriter(fileStream, Encoding.UTF8, true))
            {
                writer.Write(config.IsFirstStart);
                writer.Flush();
            }

            fileStream.Flush();
        }

        Current = config;
    }

    private void Read()
    {
        if (!File.Exists(_configPath)) Write(_defaultConfig);

        using (var fileStream = new FileStream(_configPath!, FileMode.Open, FileAccess.Read))
        {
            using (var reader = new BinaryReader(fileStream, Encoding.UTF8, true))
            {
                Current = new Config
                {
                    IsFirstStart = reader.ReadBoolean()
                };
            }
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