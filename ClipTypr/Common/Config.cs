namespace ClipTypr.Common;

using System.Text.Json;
using System.Text.Json.Serialization;

public sealed record Config
{
    [JsonPropertyName("PasteCooldownMilliseconds")]
    [JsonConverter(typeof(PasteCooldownRangeConverter))]
    public required int PasteCooldownMs { get; init; }
    public required TransferSecurity TransferSecurity { get; init; }
    public required LogLevel LogLevel { get; init; }
    [JsonPropertyName("SimulateTextPasteHotKey")]
    public required HotKey PasteHotKey { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Config), GenerationMode = JsonSourceGenerationMode.Default)]
public sealed partial class ConfigJsonContext : JsonSerializerContext { }

public sealed class PasteCooldownRangeConverter : JsonConverter<int>
{
    private const int Min = (int)TimeSpan.MillisecondsPerSecond;
    private const int Max = (int)TimeSpan.MillisecondsPerMinute;

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetInt32();
        return value >= Min && value <= Max ? value : throw new JsonException($"{value} is not a valid valeu for {nameof(Config.PasteCooldownMs)}");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}