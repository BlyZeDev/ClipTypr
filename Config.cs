namespace StrokeMyKeys;

public sealed record Config
{
    public required int PasteCooldownMs { get; init; }
}