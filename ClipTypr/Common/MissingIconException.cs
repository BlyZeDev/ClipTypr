namespace ClipTypr.Common;

public sealed class MissingIconException : Exception
{
    public MissingIconException(string message) : base(message) { }
}