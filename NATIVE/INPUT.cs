namespace StrokeMyKeys.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct INPUT
{
    public uint Type;
    public INPUT_UNION Union;
}