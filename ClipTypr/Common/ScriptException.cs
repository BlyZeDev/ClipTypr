namespace ClipTypr.Common;

public sealed class ScriptException : Exception
{
    public ScriptException(string message) : base(message) { }

    public ScriptException(string message, Exception innerException) : base(message, innerException) { }
}