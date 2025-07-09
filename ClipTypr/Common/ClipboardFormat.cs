namespace ClipTypr.Common;

public enum ClipboardFormat : uint
{
    None = 0,
    UnicodeText = Native.CF_UNICODETEXT,
    Bitmap = Native.CF_BITMAP,
    Files = Native.CF_HDROP
}