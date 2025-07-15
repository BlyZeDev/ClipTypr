namespace ClipTypr.NotifyIcon;

using System.Runtime.InteropServices;
using System.Text;

public sealed unsafe class NotifyIcon : IDisposable
{
    private const string WindowClassName = $"{nameof(ClipTypr)}NotifyIconWindow";

    private readonly nint _instanceHandle;
    private readonly nint _iconHandle;
    private readonly string _toolTip;

    private readonly Dictionary<int, Action> _menuActions;
    private readonly Dictionary<string, nint> _subMenus;

    private GCHandle thisHandle;
    private nint hWnd;
    private nint trayMenu;

    private IEnumerable<IMenuItem> currentMenuItems;
    private bool menuRefreshQueued;
    private int nextCommandId;

    public NotifyIcon(nint iconHandle, string toolTip)
    {
        _iconHandle = iconHandle;
        _toolTip = toolTip;
        _instanceHandle = Native.GetModuleHandle(null);

        _menuActions = [];
        _subMenus = [];

        var wndProc = new Native.WndProc(WndProcFunc);
        var wndClass = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
            hInstance = _instanceHandle,
            lpszClassName = WindowClassName
        };
        Native.RegisterClass(ref wndClass);

        hWnd = Native.CreateWindowEx(0, WindowClassName, "", 0, 0, 0, 0, 0, 0, 0, _instanceHandle, 0);
        thisHandle = GCHandle.Alloc(this, GCHandleType.Normal);

        Native.SetWindowLongPtr(hWnd, Native.GWLP_USERDATA, GCHandle.ToIntPtr(thisHandle));

        currentMenuItems = [];
        menuRefreshQueued = false;
        nextCommandId = 1000;
    }

    public void Run(IEnumerable<IMenuItem> menuItems, CancellationToken token)
    {
        currentMenuItems = menuItems;

        MonitorMenuItems(currentMenuItems);

        ShowIcon();
        RebuildMenu(menuItems);

        using (var registration = token.Register(() => Native.PostMessage(hWnd, Native.WM_QUIT, 0, 0)))
        {
            while (Native.GetMessage(out var message, hWnd, 0, 0))
            {
                Native.TranslateMessage(ref message);
                Native.DispatchMessage(ref message);
            }
        }
    }

    public void Dispose()
    {
        MonitorMenuItems(currentMenuItems, true);
        RemoveIcon();

        if (trayMenu != nint.Zero)
        {
            Native.DestroyMenu(trayMenu);
            trayMenu = nint.Zero;
        }

        if (hWnd != nint.Zero)
        {
            Native.SetWindowLongPtr(hWnd, Native.GWLP_USERDATA, nint.Zero);
            if (thisHandle.IsAllocated) thisHandle.Free();

            Native.DestroyWindow(hWnd);
            hWnd = nint.Zero;
        }
    }

    private void RebuildMenu(IEnumerable<IMenuItem> items)
    {
        if (trayMenu != nint.Zero)
        {
            Native.DestroyMenu(trayMenu);
            trayMenu = nint.Zero;
        }
        trayMenu = Native.CreatePopupMenu();

        _menuActions.Clear();
        _subMenus.Clear();

        nextCommandId = 1000;
        BuildMenu(trayMenu, items);
    }

    private void BuildMenu(nint menuHandle, IEnumerable<IMenuItem> items)
    {
        foreach (var item in items)
        {
            if (item is MenuItem menuItem)
            {
                if (menuItem.SubMenu?.Count > 0)
                {
                    var subMenu = Native.CreatePopupMenu();
                    BuildMenu(subMenu, menuItem.SubMenu);
                    Native.AppendMenu(menuHandle, Native.MF_POPUP | (menuItem.IsDisabled ? Native.MF_GRAYED : 0), subMenu, menuItem.Text);
                }
                else
                {
                    var id = nextCommandId++;
                    _menuActions[id] = () => menuItem.Click?.Invoke(menuItem, this);

                    var flags = Native.MF_STRING;
                    if (menuItem.IsDisabled) flags |= Native.MF_GRAYED;
                    if (menuItem.IsChecked ?? false) flags |= Native.MF_CHECKED;

                    Native.AppendMenu(menuHandle, flags, id, menuItem.Text);
                }
            }
            else Native.AppendMenu(menuHandle, Native.MF_SEPARATOR, 0, null!);
        }
    }

    private void MonitorMenuItems(IEnumerable<IMenuItem> menuItems, bool onlyDetach = false)
    {
        foreach (var item in menuItems)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.Changed -= OnMenuItemChange;
                if (!onlyDetach) menuItem.Changed += OnMenuItemChange;

                if (menuItem.SubMenu?.Count > 0) MonitorMenuItems(menuItem.SubMenu);
            }
        }
    }

    private void ShowIcon()
    {
        var iconData = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hWnd,
            uID = Native.ID_TRAY_ICON,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
            uCallbackMessage = Native.WM_APP_TRAYICON,
            hIcon = _iconHandle
        };

        var tipPtr = iconData.szTip;
        for (int i = 0; i < NOTIFYICONDATA.SZTIP_BYTE_SIZE; i++)
        {
            tipPtr[i] = 0;
        }

        var bytes = Encoding.Unicode.GetBytes(_toolTip);
        var maxBytes = NOTIFYICONDATA.SZTIP_BYTE_SIZE - 2;
        var length = Math.Min(bytes.Length, maxBytes);

        for (int i = 0; i < length; i++)
        {
            tipPtr[i] = bytes[i];
        }

        Native.Shell_NotifyIcon(Native.NIM_ADD, ref iconData);
    }

    private void RemoveIcon()
    {
        var iconData = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hWnd,
            uID = Native.ID_TRAY_ICON
        };

        Native.Shell_NotifyIcon(Native.NIM_DELETE, ref iconData);
    }

    private void OnMenuItemChange(object? sender, bool subMenuChanged)
    {
        if (menuRefreshQueued) return;

        menuRefreshQueued = true;
        Native.PostMessage(hWnd, Native.WM_APP_TRAYICON_REBUILD, 0, 0);
    }

    private nint WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Native.WM_APP_TRAYICON:
                var eventCode = (int)lParam;

                if (eventCode is Native.WM_LBUTTONUP or Native.WM_RBUTTONUP)
                {
                    Native.SetForegroundWindow(hWnd);
                    Native.GetCursorPos(out var pt);
                    Native.TrackPopupMenu(trayMenu, Native.TPM_RIGHTBUTTON, pt.x, pt.y, 0, hWnd, 0);
                }
                break;

            case Native.WM_COMMAND:
                var command = (int)(wParam & 0xFFFF);
                if (_menuActions.TryGetValue(command, out var action))
                {
                    action.Invoke();
                }
                break;

            case Native.WM_APP_TRAYICON_REBUILD:
                menuRefreshQueued = false;
                RebuildMenu(currentMenuItems);
                return nint.Zero;
        }
        
        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}