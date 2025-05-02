namespace StrokeMyKeys;

using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class Native
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";

    public const int STD_OUTPUT_HANDLE = -11;

    public const uint MB_ICONERROR = 0x00000010;
    public const uint MB_ICONQUESTION = 0x00000020;
    public const uint MB_ICONEXLAMATION = 0x00000030;
    public const uint MB_ICONINFORMATION = 0x00000040;
    public const uint MB_SYSTEMMODAL = 0x00001000;

    public const int ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x04;

    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    public const int GWL_STYLE = -16;
    public const int WS_MINIMIZEBOX = 0x00020000;

    public const int SC_CLOSE = 0xF060;

    public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const int KEYEVENTF_KEYUP = 0x0002;
    public const int KEYEVENTF_UNICODE = 0x0004;

    public const int WM_HOTKEY = 0x0312;

    public const uint CF_UNICODETEXT = 13;

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GetConsoleWindow();

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GetStdHandle(int handle);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetConsoleMode(nint hConsoleHandle, int mode);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetConsoleMode(nint handle, out int mode);

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
    public static extern nint GetSystemMenu(nint hWnd, bool bRevert);

    [DllImport(User32, SetLastError = true)]
    public static extern nint GetClipboardData(uint uFormat);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nint GlobalLock(nint hMem);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(nint hMem);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern nuint GlobalSize(nint hMem);

    [DllImport(User32, SetLastError = true)]
    public static extern unsafe uint SendInput(uint numberOfInputs, INPUT* inputs, int sizeOfInputStructure);

    public static Win32Exception? GetError()
    {
        var errorCode = Marshal.GetLastPInvokeError();
        return errorCode == 0 ? null : new Win32Exception(errorCode, Marshal.GetLastPInvokeErrorMessage());
    }

    public static void ShowMessage(nint ownerHandle, string text, string caption, uint icon)
        => _ = MessageBox(ownerHandle, text, caption, MB_SYSTEMMODAL | icon);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(nint hWnd, string text, string caption, uint type);
}