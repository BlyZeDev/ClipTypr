namespace ClipTypr.Common;

using System.Text.Json.Serialization;

public readonly record struct HotKey
{
    [JsonIgnore]
    public int Id => HashCode.Combine(Modifiers, Key);

    public required ConsoleModifiers Modifiers { get; init; }
    public required ConsoleKey Key { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(HotKey), GenerationMode = JsonSourceGenerationMode.Default)]
public sealed partial class HotKeyJsonContext : JsonSerializerContext { }