namespace StrokeMyKeys;

using System.Text;

public static class Logger
{
    private const string AnsiTextColor = "\x1b[38;2;255;255;255m";
    private const string AnsiReset = "\x1b[0m";

    private static readonly RGB InfoColor = new RGB
    {
        R = 0,
        G = 175,
        B = 255
    };
    private static readonly RGB WarningColor = new RGB
    {
        R = 255,
        G = 175,
        B = 0
    };
    private static readonly RGB ErrorColor = new RGB
    {
        R = 255,
        G = 25,
        B = 25
    };

    public static void LogInfo(string text)
        => Log(LogLevel.Info, InfoColor, text, null);

    public static void LogWarning(string text, Exception? exception = null)
        => Log(LogLevel.Warning, WarningColor, text, exception);

    public static void LogError(string text, Exception? exception)
        => Log(LogLevel.Error, ErrorColor, text, exception);

    private static void Log(LogLevel logLevel, in RGB rgb, string text, Exception? exception)
    {
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
    }

    private enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}