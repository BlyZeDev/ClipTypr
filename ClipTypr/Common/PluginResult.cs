namespace ClipTypr.Common;

using System.Diagnostics.CodeAnalysis;

public sealed record PluginResult
{
    public required int ExitCode { get; init; }
    public required string? ErrorMessage { get; init; }

    public required string? FilePath { get; init; }

    [MemberNotNullWhen(true, nameof(FilePath))]
    public bool IsSuccess => File.Exists(FilePath);
}