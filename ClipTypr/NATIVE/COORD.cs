namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct COORD
{
    public short X;
    public short Y;
}