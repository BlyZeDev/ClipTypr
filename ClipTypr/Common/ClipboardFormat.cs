namespace ClipTypr.Common;

public enum ClipboardFormat : uint
{
    None = 0,
    UnicodeText = Native.CF_UNICODETEXT,
    Bitmap = Native.CF_DIBV5,
    Files = Native.CF_HDROP
}