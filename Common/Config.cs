namespace ClipTypr.Common;

using System.Text.Json.Serialization;

public sealed record Config
{
    [JsonPropertyName("PasteCooldownMilliseconds")]
    public required uint PasteCooldownMs { get; init; }
    public required TransferSecurity TransferSecurity { get; init; }
    public required LogLevel LogLevel { get; init; }
    [JsonPropertyName("SimulatePasteHotKey")]
    public required HotKey PasteHotKey { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Config), GenerationMode = JsonSourceGenerationMode.Default)]
public sealed partial class ConfigJsonContext : JsonSerializerContext { }