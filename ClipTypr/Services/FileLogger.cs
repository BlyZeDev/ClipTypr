namespace ClipTypr.Services;

using System;
using System.Runtime.CompilerServices;
using System.Text;

public sealed class FileLogger : ILoggerTarget
{
    private readonly StreamWriter _writer;

    public FileLogger(ClipTyprContext context)
    {
        var fileStream = new FileStream(
            Path.Combine(context.LogDirectory, $"{Util.GetFileNameTimestamp()}.log"),
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);

        _writer = new StreamWriter(fileStream, Encoding.UTF8);
    }

    public void LogDebug(string text, Exception? exception = null, [CallerFilePath] string callerFilePath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = 0)
        => LogMessage(LogLevel.Debug, text, exception, new CallerInfo
        {
            CallerFilePath = callerFilePath,
            CallerMemberName = callerMemberName,
            CallerLineNumber = callerLineNumber
        });

    public void LogMessage(LogLevel logLevel, string text, Exception? exception, CallerInfo? callerInfo)
    {
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

        _writer.Write(builder.ToString());
        _writer.Flush();
    }
}