namespace ClipTypr.Services;

using System.Runtime.CompilerServices;

public interface ILogger
{
    public LogLevel LogLevel { get; set; }
    public void LogDebug(string text, Exception? exception = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0);
    public void LogInfo(string text);
    public void LogWarning(string text, Exception? exception = null);
    public void LogError(string text, Exception? exception);
    public void LogCritical(string text, Exception? exception);
}