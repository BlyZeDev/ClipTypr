namespace ClipTypr.NATIVE;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

internal static class Native
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string Shell32 = "shell32.dll";

    public const int STD_INPUT_HANDLE = -10;
    public const int STD_OUTPUT_HANDLE = -11;

    public const uint MB_ICONERROR = 0x00000010;
    public const uint MB_ICONQUESTION = 0x00000020;
    public const uint MB_ICONEXLAMATION = 0x00000030;
    public const uint MB_ICONINFORMATION = 0x00000040;
    public const uint MB_SYSTEMMODAL = 0x00001000;
    public const int MB_YESNO = 0x00000004;
    public const uint MB_HELP = 0x00004000;

    public const int IDYES = 6;

    public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x04;
    public const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    public const uint ENABLE_EXTENDED_FLAGS = 0x0080;

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    public const int GWL_STYLE = -16;
    public const int SWP_NOSIZE = 0x0001;
    public const int SWP_NOMOVE = 0x0002;
    public const int SWP_NOZORDER = 0x0004;
    public const int SWP_FRAMECHANGED = 0x0020;

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

    public const int WM_HOTKEY = 0x0312;
    public const uint PM_REMOVE = 0x0001;

    public const uint GMEM_MOVEABLE = 0x0002;
    public const uint CF_UNICODETEXT = 13;
    public const uint CF_HDROP = 15;

    [DllImport(Shell32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint ExtractIcon(nint hInst, string lpszExeFileName, int nIconIndex);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GetConsoleWindow();

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
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern int DeleteMenu(nint hMenu, int nPosition, int wFlags);

    [DllImport(User32, SetLastError = true)]
    public static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport(User32, SetLastError = true)]
    public static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport(User32, SetLastError = true)]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetSystemMenu(nint hWnd, bool bRevert);

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
    public static extern bool EmptyClipboard();

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GlobalLock(nint hMem);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(nint hMem);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nuint GlobalSize(nint hMem);

    [DllImport(Shell32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint DragQueryFile(nint hDrop, uint iFile, StringBuilder lpszFile, int cch);

    [DllImport(Shell32, SetLastError = true)]
    public static extern uint DragQueryFile(nint hDrop, uint iFile, nint lpszFile, uint cch);

    [DllImport(Shell32, SetLastError = true)]
    public static extern bool DragFinish(nint hDrop);

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetMessageExtraInfo();

    [DllImport(User32, SetLastError = true)]
    public static extern unsafe uint SendInput(uint numberOfInputs, INPUT* inputs, int sizeOfInputStructure);

    [DllImport(User32, SetLastError = true)]
    public static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport(User32, SetLastError = true)]
    public static extern nint CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport(User32, SetLastError = true)]
    public static extern bool DestroyWindow(nint hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport(User32, SetLastError = true)]
    public static extern bool PeekMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport(User32, SetLastError = true)]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport(User32, SetLastError = true)]
    public static extern nint DispatchMessage([In] ref MSG lpmsg);

    [DllImport(User32, SetLastError = true)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport(User32, SetLastError = true)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    public static Win32Exception? GetError()
    {
        var errorCode = Marshal.GetLastPInvokeError();
        return errorCode == 0 ? null : new Win32Exception(errorCode, Marshal.GetLastPInvokeErrorMessage());
    }

    public static int ShowMessage(nint ownerHandle, string text, string caption, uint flags)
        => MessageBox(ownerHandle, text, $"{(string.IsNullOrWhiteSpace(caption) ? "" : $"{nameof(ClipTypr)} - {caption}")}", MB_SYSTEMMODAL | flags);

    public static int ShowHelpMessage(nint ownerHandle, string text, string caption, uint flags, MsgBoxCallback callback)
    {
        var msgBoxParams = new MSGBOXPARAMS
        {
            cbSize = (uint)Marshal.SizeOf<MSGBOXPARAMS>(),
            hwndOwner = ownerHandle,
            hInstance = nint.Zero,
            lpszText = text,
            lpszCaption = $"{(string.IsNullOrWhiteSpace(caption) ? "" : $"{nameof(ClipTypr)} - {caption}")}",
            dwStyle = MB_SYSTEMMODAL | MB_HELP | flags,
            lpszIcon = nint.Zero,
            dwContextHelpId = nint.Zero,
            lpfnMsgBoxCallback = callback,
            dwLanguageId = 0
        };

        return MessageBoxIndirect(ref msgBoxParams);
    }

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);

    [DllImport(User32, SetLastError = true)]
    private static extern int MessageBoxIndirect(ref MSGBOXPARAMS msgboxParams);
}