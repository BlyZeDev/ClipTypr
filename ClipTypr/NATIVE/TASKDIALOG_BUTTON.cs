namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
public struct TASKDIALOG_BUTTON
{
    public int nButtonID;
    public nint pszButtonText;
}