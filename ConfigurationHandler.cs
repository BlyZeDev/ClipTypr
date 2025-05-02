namespace StrokeMyKeys;

using System.Text;

public sealed class ConfigurationHandler
{
    private static readonly Config _defaultConfig = new Config
    {
        IsFirstStart = true
    };

    private readonly string _configPath;

    public Config Current { get; private set; } = null!;

    public ConfigurationHandler()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "appdata.config");

        Read();
    }

    public void Write(Config config)
    {
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

        using (var fileStream = new FileStream(_configPath, FileMode.Open, FileAccess.Read))
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
}