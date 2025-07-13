namespace ClipTypr.Common;

public enum ClipboardFormat : uint
{
    None,
    UnicodeText = Native.CF_UNICODETEXT,
    Files = Native.CF_HDROP,
    DibV5 = Native.CF_DIBV5
}