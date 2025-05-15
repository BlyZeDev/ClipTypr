namespace StrokeMyKeys.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct HARDWAREINPUT
{
    public uint Msg;
    public ushort ParamL;
    public ushort ParamH;
}