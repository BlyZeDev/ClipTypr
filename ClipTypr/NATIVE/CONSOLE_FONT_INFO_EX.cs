namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct CONSOLE_FONT_INFO_EX
{
    public uint cbSize;
    public uint nFont;
    public COORD dwFontSize;
    public int FontFamily;
    public int FontWeight;
    public fixed char FaceName[32];
}