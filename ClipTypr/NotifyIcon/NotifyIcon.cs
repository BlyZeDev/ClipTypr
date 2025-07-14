namespace ClipTypr.NotifyIcon;

using System.Runtime.InteropServices;

public sealed unsafe class NotifyIcon : IDisposable
{
    private const string WindowClassName = $"{nameof(ClipTypr)}NotifyIconWindow";

    private readonly nint _instanceHandle;
    private readonly nint _iconHandle;

    private readonly Dictionary<int, Action> _menuActions;
    private readonly Dictionary<string, nint> _subMenus;

    private GCHandle thisHandle;
    private nint hWnd;
    private nint trayMenu;

    private int nextCommandId;

    public string ToolTip { get; set; }

    public NotifyIcon(nint iconHandle)
    {
        _iconHandle = iconHandle;
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
        var r = Native.RegisterClass(ref wndClass);

        hWnd = Native.CreateWindowEx(0, WindowClassName, "", 0, 0, 0, 0, 0, 0, 0, _instanceHandle, 0);
        thisHandle = GCHandle.Alloc(this, GCHandleType.Normal);

        Native.SetWindowLongPtr(hWnd, Native.GWLP_USERDATA, GCHandle.ToIntPtr(thisHandle));

        nextCommandId = 1000;
        ToolTip = "";
    }

    public void Run(IEnumerable<IMenuItem> items)
    {
        ShowIcon();
        RebuildMenu(items);

        while (Native.GetMessage(out var message, hWnd, 0, 0))
        {
            Native.TranslateMessage(ref message);
            Native.DispatchMessage(ref message);
        }
    }

    public void Dispose()
    {
        RemoveIcon();

        if (trayMenu != nint.Zero)
        {
            Native.DestroyWindow(trayMenu);
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
                    BuildMenu(menuHandle, menuItem.SubMenu);
                    Native.AppendMenu(menuHandle, Native.MF_POPUP | (menuItem.IsDisabled ? Native.MF_GRAYED : 0), subMenu, menuItem.Text);
                }
                else
                {
                    var id = nextCommandId++;
                    _menuActions[id] = () => menuItem.Click?.Invoke(menuItem, this);
                    Native.AppendMenu(menuHandle, Native.MF_STRING | (menuItem.IsDisabled ? Native.MF_GRAYED : 0), (nint)id, menuItem.Text);
                }
            }
            else Native.AppendMenu(menuHandle, Native.MF_SEPARATOR, 0, null!);
        }
    }

    private void ShowIcon()
    {
        var iconData = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = hWnd,
            uID = Native.ID_TRAY_ICON,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON,
            uCallbackMessage = Native.WM_APP_TRAYICON,
            hIcon = _iconHandle
        };
        
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

    private nint WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == Native.WM_APP_TRAYICON)
        {
            var eventCode = (int)lParam;

            if (eventCode is Native.WM_LBUTTONUP or Native.WM_RBUTTONUP)
            {
                Native.SetForegroundWindow(hWnd);
                Native.GetCursorPos(out var pt);
                Native.TrackPopupMenu(trayMenu, Native.TPM_RIGHTBUTTON, pt.x, pt.y, 0, hWnd, 0);
            }
        }
        else if (msg == Native.WM_COMMAND)
        {
            var command = (int)(wParam & 0xFFFF);
            if (_menuActions.TryGetValue(command, out var action))
            {
                action.Invoke();
            }
        }
        else if (msg == Native.WM_DESTROY)
        {
            Dispose();
        }

        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}