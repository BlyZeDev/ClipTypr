namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct TASKDIALOG_CONFIG
{
    public uint cbSize;
    public nint hwndParent;
    public nint hInstance;
    public uint dwFlags;
    public uint dwCommonButtons;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszWindowTitle;
    public nint hMainIcon;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszMainInstruction;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszContent;
    public uint cButtons;
    public nint pButtons;
    public int nDefaultButton;
    public uint cRadioButtons;
    public nint pRadioButtons;
    public int nDefaultRadioButton;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszVerificationText;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszExpandedInformation;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszExpandedControlText;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszCollapsedControlText;
    public nint hFooterIcon;
    [MarshalAs(UnmanagedType.LPWStr)] public string pszFooter;
    public nint pfCallback;
    public nint lpCallbackData;
    public uint cxWidth;
}