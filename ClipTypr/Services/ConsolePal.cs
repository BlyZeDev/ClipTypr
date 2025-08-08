namespace ClipTypr.Services;

using System.Runtime.InteropServices;

public sealed class ConsolePal
{
    private readonly nint _windowHandle;
    private readonly nint _stdInHandle;
    private readonly nint _stdOutHandle;

    public ConsolePal()
    {
        _windowHandle = PInvoke.GetConsoleWindow();
        _stdInHandle = PInvoke.GetStdHandle(PInvoke.STD_INPUT_HANDLE);
        _stdOutHandle = PInvoke.GetStdHandle(PInvoke.STD_OUTPUT_HANDLE);
        
        Console.TreatControlCAsInput = true;
        Console.CursorVisible = false;

        PInvoke.GetConsoleMode(_stdInHandle, out var mode);
        mode &= ~PInvoke.ENABLE_QUICK_EDIT_MODE;
        PInvoke.SetConsoleMode(_stdInHandle, mode | PInvoke.ENABLE_EXTENDED_FLAGS);

        PInvoke.GetConsoleMode(_stdOutHandle, out mode);
        PInvoke.SetConsoleMode(_stdOutHandle, mode | PInvoke.ENABLE_VIRTUAL_TERMINAL_PROCESSING);

        var fontInfo = new CONSOLE_FONT_INFO_EX
        {
            cbSize = (uint)Marshal.SizeOf<CONSOLE_FONT_INFO_EX>()
        };
        if (PInvoke.GetCurrentConsoleFontEx(_stdOutHandle, false, ref fontInfo))
        {
            fontInfo.dwFontSize = new COORD
            {
                X = 0,
                Y = (short)Math.Clamp(fontInfo.dwFontSize.Y * 1.5, 12, 24)
            };
            PInvoke.SetCurrentConsoleFontEx(_stdOutHandle, false, ref fontInfo);
        }

        var windowLong = PInvoke.GetWindowLong(_windowHandle, PInvoke.GWL_STYLE);
        windowLong &= ~(PInvoke.WS_SIZEBOX | PInvoke.WS_MINIMIZEBOX | PInvoke.WS_MAXIMIZEBOX);
        _ = PInvoke.SetWindowLong(_windowHandle, PInvoke.GWL_STYLE, windowLong);

        int x = 0, y = 0, width = 0, height = 0;
        if (PInvoke.AreDpiAwarenessContextsEqual(PInvoke.GetThreadDpiAwarenessContext(), PInvoke.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
        {
            var handle = PInvoke.MonitorFromWindow(_windowHandle, PInvoke.MONITOR_DEFAULTTONEAREST);
            if (handle != nint.Zero)
            {
                var monitorInfo = new MONITORINFO
                {
                    cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
                };

                if (PInvoke.GetMonitorInfo(handle, ref monitorInfo))
                {
                    x = monitorInfo.rcWork.Left;
                    y = monitorInfo.rcWork.Top;
                    width = (int)((monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / 1.5);
                    height = (int)((monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / 1.5);
                }
            }
        }

        var uFlags = PInvoke.SWP_NOMOVE | PInvoke.SWP_NOZORDER | PInvoke.SWP_FRAMECHANGED;
        if (x == 0 && y == 0 && width == 0 && height == 0) uFlags |= PInvoke.SWP_NOSIZE;

        PInvoke.SetWindowPos(_windowHandle, nint.Zero, x, y, width, height, uFlags);

        var sysMenu = PInvoke.GetSystemMenu(_windowHandle, false);
        _ = PInvoke.DeleteMenu(sysMenu, PInvoke.SC_MINIMIZE, PInvoke.MF_BYCOMMAND);
        _ = PInvoke.DeleteMenu(sysMenu, PInvoke.SC_MAXIMIZE, PInvoke.MF_BYCOMMAND);
        _ = PInvoke.DeleteMenu(sysMenu, PInvoke.SC_CLOSE, PInvoke.MF_BYCOMMAND);
    }

    public bool IsVisible() => PInvoke.IsWindowVisible(_windowHandle);

    public void ShowWindow() => PInvoke.ShowWindow(_windowHandle, PInvoke.SW_SHOW);

    public void HideWindow() => PInvoke.ShowWindow(_windowHandle, PInvoke.SW_HIDE);

    public unsafe void SetIcon(nint ico) => PInvoke.SendMessage(_windowHandle, PInvoke.WM_SETICON, PInvoke.ICON_BIG, ico);

    public void SetTitle(string title) => PInvoke.SetWindowText(_windowHandle, title);

    public bool SupportsModernDialog()
    {
        var moduleHandle = PInvoke.LoadLibrary("comctl32.dll");
        if (moduleHandle == nint.Zero) return false;

        var procHandle = PInvoke.GetProcAddress(moduleHandle, nameof(PInvoke.TaskDialogIndirect));
        PInvoke.FreeLibrary(moduleHandle);

        return procHandle != nint.Zero;
    }

    public unsafe string? ShowModernDialog(string title, string bigText, string? content, string? expandedText, PInvoke.TaskDialogCallbackProc? callback, params ReadOnlySpan<string> buttons)
    {
        const int ButtonIdAddition = 100;

        Span<TASKDIALOG_BUTTON> dialogButtons = stackalloc TASKDIALOG_BUTTON[buttons.Length];

        var dialogConfig = new TASKDIALOGCONFIG
        {
            cbSize = (uint)Marshal.SizeOf<TASKDIALOGCONFIG>(),
            hwndParent = _windowHandle,
            hInstance = nint.Zero,
            dwFlags = PInvoke.TDF_ENABLE_HYPERLINKS | PInvoke.TDF_SIZE_TO_CONTENT,
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
            dialogConfig.dwFlags |= PInvoke.TDF_USE_HICON_MAIN;
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

                var result = PInvoke.TaskDialogIndirect(dialogConfig, out var buttonId, out _, out _);

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

    public int ShowDialog(string title, string text, uint flags, PInvoke.MsgBoxCallback? callback = null)
    {
        if (callback is null) return PInvoke.MessageBox(_windowHandle, text, $"{(string.IsNullOrWhiteSpace(title) ? "" : $"{nameof(ClipTypr)} - {title}")}", PInvoke.MB_SYSTEMMODAL | flags);

        var msgBoxParams = new MSGBOXPARAMS
        {
            cbSize = (uint)Marshal.SizeOf<MSGBOXPARAMS>(),
            hwndOwner = _windowHandle,
            hInstance = nint.Zero,
            lpszText = text,
            lpszCaption = $"{(string.IsNullOrWhiteSpace(title) ? "" : $"{nameof(ClipTypr)} - {title}")}",
            dwStyle = PInvoke.MB_SYSTEMMODAL | PInvoke.MB_HELP | flags,
            lpszIcon = nint.Zero,
            dwContextHelpId = nint.Zero,
            lpfnMsgBoxCallback = Marshal.GetFunctionPointerForDelegate(callback),
            dwLanguageId = 0
        };

        return PInvoke.MessageBoxIndirect(ref msgBoxParams);
    }

    public void Write(string text) => Console.Write(text);

    private nint TryGetIcon()
    {
        var iconHandle = PInvoke.SendMessage(_windowHandle, PInvoke.WM_GETICON, PInvoke.ICON_BIG, nint.Zero);
        if (iconHandle == nint.Zero) iconHandle = PInvoke.SendMessage(_windowHandle, PInvoke.WM_GETICON, PInvoke.ICON_SMALL, nint.Zero);

        return iconHandle;
    }
}