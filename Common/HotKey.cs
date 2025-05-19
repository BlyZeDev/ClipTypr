namespace ClipTypr.Common;

public readonly record struct HotKey
{
    public int Id => HashCode.Combine(Modifiers, Key);

    public required ConsoleModifiers Modifiers { get; init; }
    public required ConsoleKey Key { get; init; }
}