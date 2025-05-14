namespace StrokeMyKeys.Common;

public sealed record Config
{
    public required int PasteCooldownMs { get; init; }
}