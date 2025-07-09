namespace ClipTypr.Common;

public enum ClipboardFormat : uint
{
    None = 0,
    UnicodeText = Native.CF_UNICODETEXT,
    IndependentBitmapV5 = Native.CF_DIBV5,
    Files = Native.CF_HDROP
}