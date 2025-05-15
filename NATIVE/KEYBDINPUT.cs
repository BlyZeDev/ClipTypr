namespace StrokeMyKeys.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct KEYBDINPUT
{
    public ushort KeyCode;
    public ushort Scan;
    public uint Flags;
    public uint Time;
    public nint ExtraInfo;
}