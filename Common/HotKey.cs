namespace ClipTypr.Common;

using System.Text.Json.Serialization;

public readonly record struct HotKey
{
    private const int MinId = 0x0000;
    private const int MaxId = 0xBFFF;
    private const int IdRange = MaxId - MinId + 1;

    [JsonIgnore]
    public int Id => MinId + Math.Abs(HashCode.Combine(Modifiers, Key)) % IdRange;

    public required ConsoleModifiers Modifiers { get; init; }
    public required ConsoleKey Key { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(HotKey), GenerationMode = JsonSourceGenerationMode.Default)]
public sealed partial class HotKeyJsonContext : JsonSerializerContext { }