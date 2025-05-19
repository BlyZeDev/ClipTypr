namespace ClipTypr.Common;

public sealed record Config
{
    public required int PasteCooldownMs { get; init; }
    public required LogLevel LogLevel { get; init; }
    public required HotKey PasteHotKey { get; init; }
}