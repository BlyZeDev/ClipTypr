namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct WNDCLASSEX
{
    public uint cbSize;
    public uint style;
    public nint lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public nint hInstance;
    public nint hIcon;
    public nint hCursor;
    public nint hbrBackground;
    public string lpszMenuName;
    public string lpszClassName;
    public nint hIconSm;
}