namespace ClipTypr.Services;

public interface ILoggerTarget
{
    public void LogMessage(LogLevel logLevel, string text, Exception? exception, CallerInfo? callerInfo);
}