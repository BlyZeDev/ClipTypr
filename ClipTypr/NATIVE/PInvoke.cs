namespace ClipTypr.NATIVE;

using System.ComponentModel;
using System.Runtime.InteropServices;

public static class PInvoke
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string Shell32 = "shell32.dll";
    private const string SetupApi = "setupapi.dll";
    private const string AdvApi32 = "advapi32.dll";
    private const string ComCtl32 = "comctl32.dll";
    private const string ComDlg32 = "comdlg32.dll";

    public const int STD_INPUT_HANDLE = -10;
    public const int STD_OUTPUT_HANDLE = -11;

    public const nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
    public const uint MONITOR_DEFAULTTONEAREST = 1;

    public const uint MB_ICONERROR = 0x00000010;
    public const uint MB_ICONQUESTION = 0x00000020;
    public const uint MB_ICONEXLAMATION = 0x00000030;
    public const uint MB_ICONINFORMATION = 0x00000040;
    public const uint MB_SYSTEMMODAL = 0x00001000;
    public const uint MB_OK = 0x00000000;
    public const uint MB_YESNO = 0x00000004;
    public const uint MB_HELP = 0x00004000;

    public const int IDYES = 6;

    public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x04;
    public const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    public const uint ENABLE_EXTENDED_FLAGS = 0x0080;

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    public const int GWLP_USERDATA = -21;
    public const int GWL_STYLE = -16;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public const int WS_SIZEBOX = 0x00040000;
    public const int WS_MAXIMIZEBOX = 0x00010000;
    public const int WS_MINIMIZEBOX = 0x00020000;

    public const int MF_BYCOMMAND = 0x00000000;
    public const int SC_CLOSE = 0xF060;
    public const int SC_MINIMIZE = 0xF020;
    public const int SC_MAXIMIZE = 0xF030;

    public const int INPUT_KEYBOARD = 1;
    public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const int KEYEVENTF_KEYUP = 0x0002;
    public const int KEYEVENTF_UNICODE = 0x0004;

    public const int WM_APP = 0x8000;
    public const uint WM_APP_REGHOTKEY = WM_APP + 1;
    public const uint WM_APP_UNREGHOTKEY = WM_APP + 2;
    public const uint WM_APP_TRAYICON = WM_APP + 3;
    public const uint WM_APP_TRAYICON_REBUILD = WM_APP + 4;

    public const int WM_LBUTTONUP = 0x202;
    public const int WM_RBUTTONUP = 0x205;

    public const int WM_HOTKEY = 0x0312;
    public const int WM_QUIT = 0x0012;
    public const int WM_COMMAND = 0x0111;
    public const int ID_TRAY_ICON = 1000;
    public const uint NIF_MESSAGE = 0x00000001;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;
    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint MF_STRING = 0x0000;
    public const uint MF_POPUP = 0x0010;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint MF_GRAYED = 0x0001;
    public const uint MF_CHECKED = 0x00000008;
    public const int WM_GETICON = 0x7F;
    public const int WM_SETICON = 0x80;
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x00000010;

    public const uint GMEM_MOVEABLE = 0x0002;
    public const uint BI_BITFIELDS = 3;
    public const uint CF_UNICODETEXT = 13;
    public const uint CF_HDROP = 15;
    public const uint CF_DIBV5 = 17;

    public const uint REG_SZ = 0x01;
    public const uint DIREG_DEV = 0x00000001;
    public const uint DICS_FLAG_GLOBAL = 0x00000001;
    public const uint KEY_QUERY_VALUE = 0x0001;
    public const uint DIGCF_PRESENT = 0x02;
    public const uint DIGCF_DEVICEINTERFACE = 0x10;
    public static readonly Guid GUID_DEVINTERFACE_COMPORT = new Guid("86E0D1E0-8089-11D0-9CE4-08003E301F73");

    public const uint TDF_ENABLE_HYPERLINKS = 0x0001;
    public const uint TDF_USE_HICON_MAIN = 0x0002;
    public const uint TDF_ALLOW_DIALOG_CANCELLATION = 0x0008;
    public const uint TDF_SIZE_TO_CONTENT = 0x1000000;

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetThreadDpiAwarenessContext();

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AreDpiAwarenessContextsEqual(nint dpiContextA, nint dpiContextB);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern bool FreeConsole();

    [DllImport(Kernel32, SetLastError = true)]
    public static extern bool AllocConsole();

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint LoadLibrary(string lpFileName);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern bool FreeLibrary(nint hModule);

    [DllImport(Kernel32, CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern nint GetProcAddress(nint hModule, string lpProcName);

    [DllImport(Shell32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern unsafe nint ExtractIcon(nint hInst, string lpszExeFileName, int nIconIndex);

    [DllImport(Shell32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern unsafe uint ExtractIconEx(string lpszFile, int nIconIndex, nint* phiconLarge, nint* phiconSmall, uint nIcons);

    [DllImport(User32, SetLastError = true)]
    public static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [DllImport(User32, SetLastError = true)]
    public static extern int MessageBoxIndirect(ref MSGBOXPARAMS msgboxParams);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GetConsoleWindow();

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetForegroundWindow();

    [DllImport(User32, SetLastError = true)]
    public static extern bool SetForegroundWindow(nint hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern bool GetCursorPos(out POINT pt);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GetStdHandle(int handle);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetConsoleMode(nint hConsoleHandle, uint mode);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetConsoleMode(nint handle, out uint mode);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowText(nint hWnd, string title);

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCurrentConsoleFontEx(nint consoleOutput, bool maximumWindow, ref CONSOLE_FONT_INFO_EX fontInfo);

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCurrentConsoleFontEx(nint consoleOutput, bool maximumWindow, ref CONSOLE_FONT_INFO_EX fontInfo);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern int DeleteMenu(nint hMenu, int nPosition, int wFlags);

    [DllImport(User32, SetLastError = true)]
    public static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport(User32, SetLastError = true)]
    public static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport(User32, SetLastError = true)]
    public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetSystemMenu(nint hWnd, bool bRevert);

    [DllImport(User32, SetLastError = true)]
    public static extern int GetPriorityClipboardFormat(uint[] paFormatPriorityList, int cFormats);

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetClipboardData(uint uFormat);

    [DllImport(User32, SetLastError = true)]
    public static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EmptyClipboard();

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GlobalFree(nint hMem);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GlobalLock(nint hMem);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(nint hMem);

    [DllImport(Shell32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint DragQueryFile(nint hDrop, uint iFile, nint lpszFile, uint cch);

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetMessageExtraInfo();

    [DllImport(User32, SetLastError = true)]
    public static extern unsafe uint SendInput(uint numberOfInputs, INPUT* inputs, int sizeOfInputStructure);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClass([In] ref WNDCLASS lpwc);

    [DllImport(User32, SetLastError = true)]
    public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport(User32, SetLastError = true)]
    public static extern nint CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(nint hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport(User32, SetLastError = true)]
    public static extern nint SendMessage(nint hWnd, int Msg, nint wParam, nint lParam);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport(User32, SetLastError = true)]
    public static extern nint DispatchMessage([In] ref MSG lpmsg);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport(SetupApi, SetLastError = true)]
    public static extern nint SetupDiGetClassDevs(ref Guid ClassGuid, nint Enumerator, nint hwndParent, uint Flags);

    [DllImport(SetupApi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiEnumDeviceInfo(nint DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport(SetupApi, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiGetDeviceInstanceId(nint DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, nint DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

    [DllImport(SetupApi, SetLastError = true)]
    public static extern nint SetupDiOpenDevRegKey(nint deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint scope, uint hwProfile, uint keyType, uint samDesired);

    [DllImport(AdvApi32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern unsafe int RegQueryValueEx(nint hKey, string lpValueName, nint lpReserved, out uint lpType, byte* lpData, ref uint lpcbData);

    [DllImport(AdvApi32, SetLastError = true)]
    public static extern int RegCloseKey(nint hKey);

    [DllImport(SetupApi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiDestroyDeviceInfoList(nint DeviceInfoSet);

    [DllImport(ComCtl32, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    public static extern uint TaskDialogIndirect([In] TASKDIALOGCONFIG pTaskConfig, out int pnButton, out int pnRadioButton, [MarshalAs(UnmanagedType.Bool)] out bool pfVerificationFlagChecked);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    public delegate void MsgBoxCallback(HELPINFO lpHelpInfo);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    public delegate uint TaskDialogCallbackProc([In] nint hwnd, [In] uint msg, [In] nint wParam, [In] nint lParam, [In] nint refData);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    public static Win32Exception? TryGetError()
    {
        var errorCode = Marshal.GetLastPInvokeError();
        return errorCode == 0 ? null : new Win32Exception(errorCode, Marshal.GetLastPInvokeErrorMessage());
    }
}