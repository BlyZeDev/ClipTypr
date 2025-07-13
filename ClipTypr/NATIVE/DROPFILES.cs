namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct DROPFILES
{
    public uint pFiles;
    public POINT pt;
    public bool fNC;
    public bool fWide;
}