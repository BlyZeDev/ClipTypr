namespace ClipTypr.Services;

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

        var fontInfo = new CONSOLE_FONT_INFO_EX
        {
            cbSize = (uint)Marshal.SizeOf<CONSOLE_FONT_INFO_EX>()
        };
        if (Native.GetCurrentConsoleFontEx(_stdOutHandle, false, ref fontInfo))
        {
            fontInfo.dwFontSize = new COORD
            {
                X = 0,
                Y = (short)(fontInfo.dwFontSize.Y * 1.5)
            };
            Native.SetCurrentConsoleFontEx(_stdOutHandle, false, ref fontInfo);
        }

        var windowLong = Native.GetWindowLong(_windowHandle, Native.GWL_STYLE);
        windowLong &= ~(Native.WS_SIZEBOX | Native.WS_MINIMIZEBOX | Native.WS_MAXIMIZEBOX);
        _ = Native.SetWindowLong(_windowHandle, Native.GWL_STYLE, windowLong);

        int x = 0, y = 0, width = 0, height = 0;
        if (Native.AreDpiAwarenessContextsEqual(Native.GetThreadDpiAwarenessContext(), Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
        {
            var handle = Native.MonitorFromWindow(_windowHandle, Native.MONITOR_DEFAULTTONEAREST);
            if (handle != nint.Zero)
            {
                var monitorInfo = new MONITORINFO
                {
                    cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
                };

                if (Native.GetMonitorInfo(handle, ref monitorInfo))
                {
                    x = monitorInfo.rcWork.Left;
                    y = monitorInfo.rcWork.Top;
                    width = (int)((monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / 1.5);
                    height = (int)((monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / 1.5);
                }
            }
        }

        var uFlags = Native.SWP_NOMOVE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED;
        if (x == 0 && y == 0 && width == 0 && height == 0) uFlags |= Native.SWP_NOSIZE;

        Native.SetWindowPos(_windowHandle, nint.Zero, x, y, width, height, uFlags);

        var sysMenu = Native.GetSystemMenu(_windowHandle, false);
        _ = Native.DeleteMenu(sysMenu, Native.SC_MINIMIZE, Native.MF_BYCOMMAND);
        _ = Native.DeleteMenu(sysMenu, Native.SC_MAXIMIZE, Native.MF_BYCOMMAND);
        _ = Native.DeleteMenu(sysMenu, Native.SC_CLOSE, Native.MF_BYCOMMAND);
    }

    public bool IsVisible() => Native.IsWindowVisible(_windowHandle);

    public void ShowWindow() => Native.ShowWindow(_windowHandle, Native.SW_SHOW);

    public void HideWindow() => Native.ShowWindow(_windowHandle, Native.SW_HIDE);

    public void SetIcon(string icoPath)
    {
        var iconHandle = Native.LoadImage(_windowHandle, icoPath, Native.IMAGE_ICON, 0, 0, Native.LR_LOADFROMFILE);
        if (iconHandle == nint.Zero) return;

        Native.SendMessage(_windowHandle, Native.WM_SETICON, Native.ICON_SMALL, iconHandle);
        Native.SendMessage(_windowHandle, Native.WM_SETICON, Native.ICON_BIG, iconHandle);
    }

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