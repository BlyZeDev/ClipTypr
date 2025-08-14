namespace ClipTypr.Common;

public abstract record ClipboardEntry
{
    public string DisplayText { get; }

    protected ClipboardEntry(string displayText)
    {
        DisplayText = displayText;
    }
}