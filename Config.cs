namespace StrokeMyKeys;

public sealed record Config
{
    public required bool IsFirstStart { get; init; }
}