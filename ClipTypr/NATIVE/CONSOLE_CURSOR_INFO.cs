namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct CONSOLE_CURSOR_INFO
{
    public uint dwSize;
    public bool bVisible;
}