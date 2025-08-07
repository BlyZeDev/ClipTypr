namespace ClipTypr.Services;

using System;
using System.Runtime.CompilerServices;
using System.Text;

public sealed class FileLogger : ILogger
{
    public event Action<LogLevel, string, Exception?>? Log;

    public LogLevel LogLevel { get; set; }

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

        var builder = new StringBuilder();
        builder.Append($"{DateTime.Now:dd.MM.yyyy HH:mm:ss.ffff} | ");
        builder.Append(logLevel.ToString());

        if (callerInfo is not null) builder.Append($" | {callerInfo}");

        builder.AppendLine($" | {text}");

        if (exception is not null)
        {
            builder.AppendLine("Exception");
            builder.AppendLine(exception.ToString());
        }

        //_console.Write(builder.ToString());
        Log?.Invoke(logLevel, text, exception);
    }
}