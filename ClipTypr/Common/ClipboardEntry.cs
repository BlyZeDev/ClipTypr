namespace ClipTypr.Common;

public abstract record ClipboardEntry
{
    public DateTime Timestamp { get; }

    public string DisplayText { get; }

    protected ClipboardEntry(DateTime timestamp, string displayText)
    {
        Timestamp = timestamp;
        DisplayText = displayText;
    }
}