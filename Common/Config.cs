namespace ClipTypr.Common;

using System.Text.Json.Serialization;

public sealed record Config
{
    public required uint PasteCooldownMs { get; init; }
    public required LogLevel LogLevel { get; init; }
    public required HotKey PasteHotKey { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Config), GenerationMode = JsonSourceGenerationMode.Default)]
public sealed partial class ConfigJsonContext : JsonSerializerContext { }