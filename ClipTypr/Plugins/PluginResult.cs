namespace ClipTypr.Plugins;

using System.Diagnostics.CodeAnalysis;

public sealed record PluginResult
{
    public required ScriptException? Error { get; init; }

    public required string? FilePath { get; init; }

    [MemberNotNullWhen(true, nameof(FilePath))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => File.Exists(FilePath);
}