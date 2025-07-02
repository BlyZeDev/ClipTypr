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
                Y = (short)Math.Clamp(fontInfo.dwFontSize.Y * 1.5, 12, 24)
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

    public unsafe void SetIcon(string icoPath)
    {
        var smallIcon = stackalloc nint[1];
        var largeIcon = stackalloc nint[1];

        _ = Native.ExtractIconEx(icoPath, 0, largeIcon, smallIcon, 1);

        if (smallIcon[0] != nint.Zero) Native.SendMessage(_windowHandle, Native.WM_SETICON, Native.ICON_SMALL, smallIcon[0]);
        if (largeIcon[0] != nint.Zero) Native.SendMessage(_windowHandle, Native.WM_SETICON, Native.ICON_BIG, largeIcon[0]);
    }

    public void SetTitle(string title) => Native.SetWindowText(_windowHandle, title);

    public bool SupportsModernDialog()
    {
        var moduleHandle = Native.LoadLibrary("comctl32.dll");
        if (moduleHandle == nint.Zero) return false;

        var procHandle = Native.GetProcAddress(moduleHandle, nameof(Native.TaskDialogIndirect));
        Native.FreeLibrary(moduleHandle);

        return procHandle != nint.Zero;
    }

    public unsafe string? ShowModernDialog(string title, string bigText, string? content, string? expandedText, Native.TaskDialogCallbackProc? callback, params ReadOnlySpan<string> buttons)
    {
        const int ButtonIdAddition = 100;

        Span<TASKDIALOG_BUTTON> dialogButtons = stackalloc TASKDIALOG_BUTTON[buttons.Length];

        var dialogConfig = new TASKDIALOGCONFIG
        {
            cbSize = (uint)Marshal.SizeOf<TASKDIALOGCONFIG>(),
            hwndParent = _windowHandle,
            hInstance = nint.Zero,
            dwFlags = Native.TDF_ENABLE_HYPERLINKS | Native.TDF_SIZE_TO_CONTENT,
            mainIcon = nint.Zero,
            dwCommonButtons = 0,
            pszWindowTitle = Marshal.StringToCoTaskMemUni($"{(string.IsNullOrWhiteSpace(title) ? "" : $"{nameof(ClipTypr)} - {title}")}"),
            pszMainInstruction = Marshal.StringToCoTaskMemUni(bigText),
            pszContent = Marshal.StringToCoTaskMemUni(content),
            pszCollapsedControlText = Marshal.StringToCoTaskMemUni("Show information"),
            pszExpandedControlText = Marshal.StringToCoTaskMemUni("Hide information"),
            pszExpandedInformation = Marshal.StringToCoTaskMemUni(expandedText),
            nDefaultButton = 0,
            pRadioButtons = nint.Zero,
            cRadioButtons = 0,
            nDefaultRadioButton = 0,
            footerIcon = nint.Zero,
            pszFooter = nint.Zero,
            pszVerificationText = nint.Zero,
            pfCallbackProc = nint.Zero,
            lpCallbackData = nint.Zero,
            cxWidth = 0
        };

        if (callback is not null)
        {
            dialogConfig.pfCallbackProc = Marshal.GetFunctionPointerForDelegate(callback);
            dialogConfig.lpCallbackData = nint.Zero;
        }

        var iconHandle = TryGetIcon();
        if (iconHandle != nint.Zero)
        {
            dialogConfig.dwFlags |= Native.TDF_USE_HICON_MAIN;
            dialogConfig.mainIcon = iconHandle;
        }

        try
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                dialogButtons[i] = new TASKDIALOG_BUTTON
                {
                    nButtonID = i + ButtonIdAddition,
                    pszButtonText = Marshal.StringToCoTaskMemUni(buttons[i])
                };
            }

            fixed (TASKDIALOG_BUTTON* dialogButtonsPtr = dialogButtons)
            {
                dialogConfig.pButtons = (nint)dialogButtonsPtr;
                dialogConfig.cButtons = (uint)dialogButtons.Length;

                var result = Native.TaskDialogIndirect(dialogConfig, out var buttonId, out _, out _);

                return result != 0 ? null : Marshal.PtrToStringUni(dialogButtons[buttonId - ButtonIdAddition].pszButtonText);
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(dialogConfig.pszWindowTitle);
            Marshal.FreeCoTaskMem(dialogConfig.pszContent);
            Marshal.FreeCoTaskMem(dialogConfig.pszMainInstruction);
            Marshal.FreeCoTaskMem(dialogConfig.pszCollapsedControlText);
            Marshal.FreeCoTaskMem(dialogConfig.pszExpandedControlText);
            Marshal.FreeCoTaskMem(dialogConfig.pszExpandedInformation);

            foreach (ref readonly var button in dialogButtons)
            {
                Marshal.FreeCoTaskMem(button.pszButtonText);
            }
        }
    }

    public int ShowDialog(string title, string text, uint flags, Native.MsgBoxCallback? callback = null)
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
            lpfnMsgBoxCallback = Marshal.GetFunctionPointerForDelegate(callback),
            dwLanguageId = 0
        };

        return Native.MessageBoxIndirect(ref msgBoxParams);
    }

    public void Write(string text) => Console.Write(text);

    private nint TryGetIcon()
    {
        var iconHandle = Native.SendMessage(_windowHandle, Native.WM_GETICON, Native.ICON_BIG, nint.Zero);
        if (iconHandle == nint.Zero) iconHandle = Native.SendMessage(_windowHandle, Native.WM_GETICON, Native.ICON_SMALL, nint.Zero);

        return iconHandle;
    }
}