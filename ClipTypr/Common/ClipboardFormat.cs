namespace ClipTypr.Common;

public readonly record struct ClipboardFormat
{
    private const uint CustomSpaceStart = 0xC000;

    public static ClipboardFormat None => new ClipboardFormat();
    public static ClipboardFormat Text => new ClipboardFormat(Native.CF_TEXT);
    public static ClipboardFormat Bitmap => new ClipboardFormat(Native.CF_BITMAP);
    public static ClipboardFormat OemText => new ClipboardFormat(Native.CF_OEMTEXT);
    public static ClipboardFormat Dib => new ClipboardFormat(Native.CF_DIB);
    public static ClipboardFormat UnicodeText => new ClipboardFormat(Native.CF_UNICODETEXT);
    public static ClipboardFormat EnhMetafile => new ClipboardFormat(Native.CF_ENHMETAFILE);
    public static ClipboardFormat Files => new ClipboardFormat(Native.CF_HDROP);
    public static ClipboardFormat Locale => new ClipboardFormat(Native.CF_LOCALE);
    public static ClipboardFormat DibV5 => new ClipboardFormat(Native.CF_DIBV5);

    public static ClipboardFormat Custom => new ClipboardFormat(uint.MaxValue);

    private readonly uint _value;

    public bool IsNone => _value == uint.MinValue;
    public bool IsText => _value == UnicodeText || _value == Text || _value == OemText;
    public bool IsImage => _value == DibV5 || _value == Dib || _value == Bitmap;
    public bool IsFiles => _value == Files;
    public bool IsCustom => _value >= CustomSpaceStart;

    public ClipboardFormat() => _value = uint.MinValue;

    public ClipboardFormat(uint value) => _value = value;

    public override string ToString()
    {
        return _value switch
        {
            var _ when _value == None => nameof(None),
            var _ when _value == Text => nameof(Text),
            var _ when _value == Bitmap => nameof(Bitmap),
            var _ when _value == OemText => nameof(OemText),
            var _ when _value == Dib => nameof(Dib),
            var _ when _value == UnicodeText => nameof(UnicodeText),
            var _ when _value == EnhMetafile => nameof(EnhMetafile),
            var _ when _value == Files => nameof(Files),
            var _ when _value == Locale => nameof(Locale),
            var _ when _value == DibV5 => nameof(DibV5),
            _ => $"Custom{_value:X2}"
        };
    }

    public static implicit operator uint(ClipboardFormat self) => self._value;
}