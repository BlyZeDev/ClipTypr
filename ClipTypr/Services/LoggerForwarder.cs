namespace ClipTypr.Services;

using System.Runtime.CompilerServices;

public sealed class LoggerForwarder : ILogger
{
    private readonly IEnumerable<ILoggerTarget> _targets;

    public LogLevel LogLevel
    {
        get => field;
        set
        {
            if (!Enum.IsDefined(value)) return;
            field = value;
        }
    }

    public event Action<LogLevel, string, Exception?>? Log;

    public LoggerForwarder(IEnumerable<ILoggerTarget> targets) => _targets = targets;

    public void LogDebug(string text, Exception? exception = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0)
        => LogMessage(LogLevel.Debug, text, exception, new CallerInfo
        {
            CallerFilePath = callerFilePath,
            CallerMemberName = callerMemberName,
            CallerLineNumber = callerLineNumber
        });

    public void LogInfo(string text) => LogMessage(LogLevel.Info, text, null, null);
    public void LogWarning(string text, Exception? exception = null) => LogMessage(LogLevel.Warning, text, exception, null);
    public void LogError(string text, Exception? exception) => LogMessage(LogLevel.Error, text, exception, null);
    public void LogCritical(string text, Exception? exception) => LogMessage(LogLevel.Critical, text, exception, null);

    private void LogMessage(LogLevel logLevel, string text, Exception? exception, CallerInfo? callerInfo)
    {
        if (logLevel < LogLevel) return;

        foreach (var target in _targets)
        {
            target.LogMessage(logLevel, text, exception, callerInfo);
        }

        Log?.Invoke(logLevel, text, exception);
    }
}