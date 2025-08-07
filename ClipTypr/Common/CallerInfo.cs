namespace ClipTypr.Common;

public sealed record CallerInfo
{
    public required string CallerFilePath { get; init; }
    public required string CallerMemberName { get; init; }
    public required int CallerLineNumber { get; init; }

    public override string ToString() => $"{Path.GetFileNameWithoutExtension(CallerFilePath)}.{CallerMemberName} line {CallerLineNumber}";
}