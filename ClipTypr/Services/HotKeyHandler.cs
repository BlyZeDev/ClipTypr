namespace ClipTypr.Services;

using System.Runtime.InteropServices;

public sealed class HotKeyHandler : IDisposable
{
    private const string WindowClassName = $"{nameof(ClipTypr)}HiddenWindow";
    private const uint WM_APP_REGHOTKEY = Native.WM_APP + 1;
    private const uint WM_APP_UNREGHOTKEY = Native.WM_APP + 2;

    private readonly ILogger _logger;
    private readonly HashSet<nint> _registeredHotkeys;
    private readonly Thread _messageThread;

    private volatile nint hWnd;

    public bool IsReady => hWnd != nint.Zero;

    public event EventHandler? Ready;
    public event EventHandler<HotKey>? HotKeyPressed;

    public HotKeyHandler(ILogger logger)
    {
        _logger = logger;

        _registeredHotkeys = [];
        _messageThread = new Thread(() =>
        {
            var wndProc = new Native.WndProc(WndProcFunc);

            var wndClassEx = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = nint.Zero,
                hIcon = nint.Zero,
                hCursor = nint.Zero,
                hbrBackground = nint.Zero,
                lpszMenuName = null!,
                lpszClassName = WindowClassName,
                hIconSm = nint.Zero
            };

            if (Native.RegisterClassEx(ref wndClassEx) == 0)
            {
                _logger.LogError("Could not register the class", Native.TryGetError());
                return;
            }

            hWnd = Native.CreateWindowEx(0, WindowClassName, "", 0, 0, 0, 0, 0, new nint(-3), nint.Zero, nint.Zero, nint.Zero);

            Ready?.Invoke(this, EventArgs.Empty);

            while (Native.GetMessage(out var msg, hWnd, 0, 0))
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessage(ref msg);
            }

            Native.DestroyWindow(hWnd);
            Interlocked.Exchange(ref hWnd, nint.Zero);
        });
        _messageThread.Start();
    }

    public void RegisterHotKey(in HotKey hotkey)
    {
        if (hWnd == nint.Zero)
        {
            _logger.LogWarning("The message thread is not initialized");
            return;
        }

        Native.PostMessage(hWnd, WM_APP_REGHOTKEY, nint.Zero, Pack(hotkey));
    }

    public void UnregisterHotKey(in HotKey hotkey)
    {
        if (hWnd == nint.Zero)
        {
            _logger.LogWarning("The message thread is not initialized");
            return;
        }

        Native.PostMessage(hWnd, WM_APP_UNREGHOTKEY, nint.Zero, Pack(hotkey));
    }

    public void Dispose()
    {
        foreach (var hotkey in _registeredHotkeys)
        {
            Native.PostMessage(hWnd, WM_APP_UNREGHOTKEY, nint.Zero, hotkey);
        }
        
        Native.PostMessage(hWnd, Native.WM_QUIT, nint.Zero, nint.Zero);
        if (_messageThread.IsAlive) _messageThread.Join();
    }

    private nint WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Native.WM_HOTKEY: HotKeyPressed?.Invoke(this, Unpack(lParam)); break;
            case WM_APP_REGHOTKEY:
                {
                    var hotkey = Unpack(lParam);

                    if (Native.RegisterHotKey(hWnd, hotkey.Id, (uint)hotkey.Modifiers, (uint)hotkey.Key))
                    {
                        _registeredHotkeys.Add(lParam);
                        _logger.LogInfo($"Registered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
                        _logger.LogDebug(hotkey.ToString());
                    }
                    else _logger.LogError($"Cannot register the hotkey: {hotkey.Modifiers} - {hotkey.Key}", Native.TryGetError());
                }
                break;
            case WM_APP_UNREGHOTKEY:
                {
                    var hotkey = Unpack(lParam);

                    if (Native.UnregisterHotKey(hWnd, hotkey.Id))
                    {
                        _registeredHotkeys.Remove(lParam);
                        _logger.LogInfo($"Unregistered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
                        _logger.LogDebug(hotkey.ToString());
                    }
                    else _logger.LogError($"Cannot unregister the hotkey: {hotkey.Id}", Native.TryGetError());
                }
                break;
        }

        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static nint Pack(in HotKey hotkey) => (nint)(((uint)hotkey.Key << 16) | ((uint)hotkey.Modifiers & 0xFFFF));

    private static HotKey Unpack(nint hotkey)
    {
        var value = hotkey.ToInt64();

        return new HotKey
        {
            Modifiers = (ConsoleModifiers)(uint)(value & 0xFFFF),
            Key = (ConsoleKey)(uint)((value >> 16) & 0xFFFF)
        };
    }
}