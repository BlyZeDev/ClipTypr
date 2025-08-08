namespace ClipTypr.Common;

public enum ClipboardFormat : uint
{
    None,
    UnicodeText = PInvoke.CF_UNICODETEXT,
    Files = PInvoke.CF_HDROP,
    DibV5 = PInvoke.CF_DIBV5
}