namespace StrokeMyKeys;

using StrokeMyKeys.Common;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config), GenerationMode = JsonSourceGenerationMode.Default)]
public sealed partial class ConfigJsonContext : JsonSerializerContext { }