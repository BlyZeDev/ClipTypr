namespace ClipTypr.NATIVE;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
public class TASKDIALOGCONFIG
{
    public uint cbSize;
    public nint hwndParent;
    public nint hInstance;
    public uint dwFlags;
    public uint dwCommonButtons;
    public nint pszWindowTitle;
    public nint mainIcon;
    public nint pszMainInstruction;
    public nint pszContent;
    public uint cButtons;
    public nint pButtons;
    public int nDefaultButton;
    public uint cRadioButtons;
    public nint pRadioButtons;
    public int nDefaultRadioButton;
    public nint pszVerificationText;
    public nint pszExpandedInformation;
    public nint pszExpandedControlText;
    public nint pszCollapsedControlText;
    public nint footerIcon;
    public nint pszFooter;
    public nint pfCallbackProc;
    public nint lpCallbackData;
    public uint cxWidth;
}