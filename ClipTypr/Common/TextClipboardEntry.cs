namespace ClipTypr.Common;

public sealed record TextClipboardEntry : ClipboardEntry
{
    public string Text { get; }

    public TextClipboardEntry(string text) : base(DateTime.UtcNow, GetDisplayText(text)) => Text = text;

    private static string GetDisplayText(string text)
    {
        var sanitized = text.Replace(Environment.NewLine, "");

        return $"✏️ - {(sanitized.Length > 20 ? $"{sanitized[..20]}..." : sanitized)}";
    }
}