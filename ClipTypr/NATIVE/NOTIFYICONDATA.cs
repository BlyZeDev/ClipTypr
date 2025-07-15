namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct NOTIFYICONDATA
{
    public const int SZTIP_BYTE_SIZE = 128;

    public uint cbSize;
    public nint hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public nint hIcon;
    public fixed byte szTip[SZTIP_BYTE_SIZE];
    public uint dwState;
    public uint dwStateMask;
    public fixed byte szInfo[256];
    public uint uTimeoutOrVersion;
    public fixed byte szInfoTitle[64];
    public uint dwInfoFlags;
}