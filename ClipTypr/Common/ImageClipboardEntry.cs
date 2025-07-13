namespace ClipTypr.Common;

using System.Drawing;

public sealed record ImageClipboardEntry : ClipboardEntry
{
    public Bitmap Image { get; }

    public ImageClipboardEntry(Bitmap bitmap) : base(DateTime.UtcNow, $"🖼️ - {bitmap.Width}x{bitmap.Height}") => Image = bitmap;
}