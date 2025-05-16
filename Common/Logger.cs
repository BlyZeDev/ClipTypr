namespace ClipTypr.Common;

using System.Diagnostics.CodeAnalysis;
using System.Text;

public static class Logger
{
    private const string AnsiTextColor = "\x1b[38;2;255;255;255m";
    private const string AnsiReset = "\x1b[0m";

    private static readonly Dictionary<LogLevel, RGB> _logLevelColors = new Dictionary<LogLevel, RGB>
    {
        { LogLevel.Debug, new RGB(255, 255, 255) },
        { LogLevel.Info, new RGB(0, 175, 255) },
        { LogLevel.Warning, new RGB(255, 175, 0) },
        { LogLevel.Error, new RGB(255, 25, 25) }
    };

    private static LogLevel _logLevel;
    public static LogLevel LogLevel //Set LogLevel on config change
    {
        get => _logLevel;
        set
        {
            if (!Enum.IsDefined(value)) return;

            _logLevel = value;
        }
    }

    public static void LogDebug(string text)
        => Log(LogLevel.Debug, text, null);

    public static void LogInfo(string text)
        => Log(LogLevel.Info, text, null);

    public static void LogWarning(string text, Exception? exception = null)
        => Log(LogLevel.Warning, text, exception);

    public static void LogError(string text, Exception? exception)
        => Log(LogLevel.Error, text, exception);

    private static void Log(LogLevel logLevel, string text, Exception? exception)
    {
        if (logLevel < LogLevel) return;

        var rgb = _logLevelColors[logLevel];

        var builder = new StringBuilder();
        builder.Append($"{AnsiTextColor}{DateTime.Now:dd.MM.yyyy HH:mm:ss.ffff} | {AnsiReset}");
        builder.Append($"\x1b[38;2;{byte.MaxValue - rgb.R};{byte.MaxValue - rgb.G};{byte.MaxValue - rgb.B}m\x1b[48;2;{rgb.R};{rgb.G};{rgb.B}m{logLevel}{AnsiReset}");
        builder.AppendLine($"{AnsiTextColor} | {text}{AnsiReset}");

        if (exception is not null)
        {
            builder.AppendLine($"{AnsiTextColor}\x1b[48;2;{rgb.R};{rgb.G};{rgb.B}m\x1b[4mException{AnsiReset}");
            builder.AppendLine($"{AnsiTextColor}{exception.ToString()}{AnsiReset}");
        }

        Console.Write(builder.ToString());
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