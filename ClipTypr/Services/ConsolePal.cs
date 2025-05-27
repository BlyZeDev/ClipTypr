namespace ClipTypr.Services;

using System;
using System.Runtime.InteropServices;

public sealed class ConsolePal
{
    private readonly nint _windowHandle;
    private readonly nint _stdInHandle;
    private readonly nint _stdOutHandle;

    public ConsolePal()
    {
        _windowHandle = Native.GetConsoleWindow();
        _stdInHandle = Native.GetStdHandle(Native.STD_INPUT_HANDLE);
        _stdOutHandle = Native.GetStdHandle(Native.STD_OUTPUT_HANDLE);
        
        Console.TreatControlCAsInput = true;
        Console.CursorVisible = false;

        Native.GetConsoleMode(_stdInHandle, out var mode);
        mode &= ~Native.ENABLE_QUICK_EDIT_MODE;
        Native.SetConsoleMode(_stdInHandle, mode | Native.ENABLE_EXTENDED_FLAGS);

        Native.GetConsoleMode(_stdOutHandle, out mode);
        Native.SetConsoleMode(_stdOutHandle, mode | Native.ENABLE_VIRTUAL_TERMINAL_PROCESSING);

        var windowLong = Native.GetWindowLong(_windowHandle, Native.GWL_STYLE);
        windowLong &= ~(Native.WS_SIZEBOX | Native.WS_MINIMIZEBOX | Native.WS_MAXIMIZEBOX);
        _ = Native.SetWindowLong(_windowHandle, Native.GWL_STYLE, windowLong);

        Native.SetWindowPos(_windowHandle, nint.Zero, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED);

        var sysMenu = Native.GetSystemMenu(_windowHandle, false);
        _ = Native.DeleteMenu(sysMenu, Native.SC_MINIMIZE, Native.MF_BYCOMMAND);
        _ = Native.DeleteMenu(sysMenu, Native.SC_MAXIMIZE, Native.MF_BYCOMMAND);
        _ = Native.DeleteMenu(sysMenu, Native.SC_CLOSE, Native.MF_BYCOMMAND);
    }

    public bool IsVisible() => Native.IsWindowVisible(_windowHandle);

    public void ShowWindow() => Native.ShowWindow(_windowHandle, Native.SW_SHOW);

    public void HideWindow() => Native.ShowWindow(_windowHandle, Native.SW_HIDE);

    public void SetTitle(string title) => Native.SetWindowText(_windowHandle, title);

    public int ShowDialog(string title, string text, uint flags, MsgBoxCallback? callback = null)
    {
        if (callback is null) return Native.MessageBox(_windowHandle, text, $"{(string.IsNullOrWhiteSpace(title) ? "" : $"{nameof(ClipTypr)} - {title}")}", Native.MB_SYSTEMMODAL | flags);

        var msgBoxParams = new MSGBOXPARAMS
        {
            cbSize = (uint)Marshal.SizeOf<MSGBOXPARAMS>(),
            hwndOwner = _windowHandle,
            hInstance = nint.Zero,
            lpszText = text,
            lpszCaption = $"{(string.IsNullOrWhiteSpace(title) ? "" : $"{nameof(ClipTypr)} - {title}")}",
            dwStyle = Native.MB_SYSTEMMODAL | Native.MB_HELP | flags,
            lpszIcon = nint.Zero,
            dwContextHelpId = nint.Zero,
            lpfnMsgBoxCallback = callback,
            dwLanguageId = 0
        };

        return Native.MessageBoxIndirect(ref msgBoxParams);
    }

    public void Write(string text) => Console.Write(text);
}