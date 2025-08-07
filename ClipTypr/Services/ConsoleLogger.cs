namespace ClipTypr.Services;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

public sealed class ConsoleLogger : ILogger
{
    private const string AnsiTextColor = "\x1b[38;2;255;255;255m";
    private const string AnsiReset = "\x1b[0m";

    private static readonly Dictionary<LogLevel, RGB> _logLevelColors = new Dictionary<LogLevel, RGB>
    {
        { LogLevel.Debug, new RGB(255, 255, 255) },
        { LogLevel.Info, new RGB(0, 175, 255) },
        { LogLevel.Warning, new RGB(255, 175, 0) },
        { LogLevel.Error, new RGB(255, 25, 25) },
        { LogLevel.Critical, new RGB(125, 0, 0) }
    };

    private readonly ConsolePal _console;

    public event Action<LogLevel, string, Exception?>? Log;

    public LogLevel LogLevel { get; set; }

    public ConsoleLogger(ConsolePal console) => _console = console;

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

        var rgb = _logLevelColors[logLevel];

        var builder = new StringBuilder();
        builder.Append($"{AnsiTextColor}{DateTime.Now:dd.MM.yyyy HH:mm:ss.ffff} | {AnsiReset}");
        builder.Append($"\x1b[38;2;{byte.MaxValue - rgb.R};{byte.MaxValue - rgb.G};{byte.MaxValue - rgb.B}m\x1b[48;2;{rgb.R};{rgb.G};{rgb.B}m{logLevel}{AnsiReset}");
        
        if (callerInfo is not null) builder.Append($"{AnsiTextColor} | {callerInfo}{AnsiReset}");
        
        builder.AppendLine($"{AnsiTextColor} | {text}{AnsiReset}");

        if (exception is not null)
        {
            builder.AppendLine($"{AnsiTextColor}\x1b[48;2;{rgb.R};{rgb.G};{rgb.B}m\x1b[4mException{AnsiReset}");
            builder.AppendLine($"{AnsiTextColor}{exception.ToString()}{AnsiReset}");
        }

        _console.Write(builder.ToString());
        Log?.Invoke(logLevel, text, exception);
    }

    private readonly record struct RGB
    {
        public required byte R { get; init; }
        public required byte G { get; init; }
        public required byte B { get; init; }

        [SetsRequiredMembers]
        public RGB(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }
}