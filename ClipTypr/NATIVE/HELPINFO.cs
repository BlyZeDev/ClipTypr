namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct HELPINFO
{
    public uint cbSize;
    public int iContextType;
    public int iCtrlId;
    public nint hItemHandle;
    public nint dwContextId;
    public POINT MousePos;
};