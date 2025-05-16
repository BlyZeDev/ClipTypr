namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct MSGBOXPARAMS
{
    public uint cbSize;
    public nint hwndOwner;
    public nint hInstance;
    public string lpszText;
    public string lpszCaption;
    public uint dwStyle;
    public nint lpszIcon;
    public nint dwContextHelpId;
    public MsgBoxCallback lpfnMsgBoxCallback;
    public uint dwLanguageId;
};

public delegate void MsgBoxCallback(HELPINFO lpHelpInfo);