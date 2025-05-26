namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct TASKDIALOG_BUTTON
{
    public int nButtonID;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pszButtonText;
}