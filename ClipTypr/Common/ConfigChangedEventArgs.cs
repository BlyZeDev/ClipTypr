namespace ClipTypr.Common;

public sealed class ConfigChangedEventArgs : EventArgs
{
    public required Config OldConfig { get; init; }
    public required Config NewConfig { get; init; }
}