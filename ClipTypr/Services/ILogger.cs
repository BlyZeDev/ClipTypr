namespace ClipTypr.Services;

public interface ILogger
{
    public LogLevel LogLevel { get; set; }
    public void LogDebug(string text);
    public void LogInfo(string text);
    public void LogWarning(string text, Exception? exception = null);
    public void LogError(string text, Exception? exception);
    public void LogImportant(string text);
}