namespace ClipTypr.Services;

public sealed class ConsolePal
{
    private readonly nint _stdInHandle;
    private readonly nint _stdOutHandle;

    public nint Handle { get; }

    public ConsolePal()
    {
        Handle = Native.GetConsoleWindow();
        _stdInHandle = Native.GetStdHandle(Native.STD_INPUT_HANDLE);
        _stdOutHandle = Native.GetStdHandle(Native.STD_OUTPUT_HANDLE);
        
        Console.TreatControlCAsInput = true;
        Console.CursorVisible = false;

        Native.GetConsoleMode(_stdInHandle, out var mode);
        mode &= ~Native.ENABLE_QUICK_EDIT_MODE;
        Native.SetConsoleMode(_stdInHandle, mode | Native.ENABLE_EXTENDED_FLAGS);

        Native.GetConsoleMode(_stdOutHandle, out mode);
        Native.SetConsoleMode(_stdOutHandle, mode | Native.ENABLE_VIRTUAL_TERMINAL_PROCESSING);

        var windowLong = Native.GetWindowLong(Handle, Native.GWL_STYLE);
        windowLong &= ~(Native.WS_SIZEBOX | Native.WS_MINIMIZEBOX | Native.WS_MAXIMIZEBOX);
        _ = Native.SetWindowLong(Handle, Native.GWL_STYLE, windowLong);

        Native.SetWindowPos(Handle, nint.Zero, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_FRAMECHANGED);

        var sysMenu = Native.GetSystemMenu(Handle, false);
        _ = Native.DeleteMenu(sysMenu, Native.SC_MINIMIZE, Native.MF_BYCOMMAND);
        _ = Native.DeleteMenu(sysMenu, Native.SC_MAXIMIZE, Native.MF_BYCOMMAND);
        _ = Native.DeleteMenu(sysMenu, Native.SC_CLOSE, Native.MF_BYCOMMAND);
    }

    public bool IsVisible() => Native.IsWindowVisible(Handle);

    public void Show() => Native.ShowWindow(Handle, Native.SW_SHOW);

    public void Hide() => Native.ShowWindow(Handle, Native.SW_HIDE);

    public void SetTitle(string title) => Native.SetWindowText(Handle, title);

    public void Write(string text) => Console.Write(text);
}