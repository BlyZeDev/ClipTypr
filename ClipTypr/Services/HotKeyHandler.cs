namespace ClipTypr.Services;

using System.Runtime.InteropServices;

public sealed class HotKeyHandler : IDisposable
{
    private const string WindowClassName = $"{nameof(ClipTypr)}HiddenWindow";

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
            var wndProc = new PInvoke.WndProc(WndProcFunc);

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

            if (PInvoke.RegisterClassEx(ref wndClassEx) == 0)
            {
                _logger.LogError("Could not register the class", PInvoke.TryGetError());
                return;
            }

            hWnd = PInvoke.CreateWindowEx(0, WindowClassName, "", 0, 0, 0, 0, 0, new nint(-3), nint.Zero, nint.Zero, nint.Zero);

            Ready?.Invoke(this, EventArgs.Empty);

            while (PInvoke.GetMessage(out var msg, hWnd, 0, 0))
            {
                PInvoke.TranslateMessage(ref msg);
                PInvoke.DispatchMessage(ref msg);
            }

            PInvoke.DestroyWindow(hWnd);
            Interlocked.Exchange(ref hWnd, nint.Zero);

            GC.KeepAlive(wndProc);
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

        PInvoke.PostMessage(hWnd, PInvoke.WM_APP_REGHOTKEY, nint.Zero, Pack(hotkey));
    }

    public void UnregisterHotKey(in HotKey hotkey)
    {
        if (hWnd == nint.Zero)
        {
            _logger.LogWarning("The message thread is not initialized");
            return;
        }

        PInvoke.PostMessage(hWnd, PInvoke.WM_APP_UNREGHOTKEY, nint.Zero, Pack(hotkey));
    }

    public void Dispose()
    {
        foreach (var hotkey in _registeredHotkeys)
        {
            PInvoke.PostMessage(hWnd, PInvoke.WM_APP_UNREGHOTKEY, nint.Zero, hotkey);
        }
        
        PInvoke.PostMessage(hWnd, PInvoke.WM_QUIT, nint.Zero, nint.Zero);
        if (_messageThread.IsAlive) _messageThread.Join();

        GC.SuppressFinalize(this);
    }

    private nint WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case PInvoke.WM_HOTKEY: HotKeyPressed?.Invoke(this, Unpack(lParam)); break;
            case PInvoke.WM_APP_REGHOTKEY:
                {
                    var hotkey = Unpack(lParam);

                    if (PInvoke.RegisterHotKey(hWnd, hotkey.Id, (uint)hotkey.Modifiers, (uint)hotkey.Key))
                    {
                        _registeredHotkeys.Add(lParam);
                        _logger.LogInfo($"Registered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
                        _logger.LogDebug(hotkey.ToString());
                    }
                    else _logger.LogError($"Cannot register the hotkey: {hotkey.Modifiers} - {hotkey.Key}", PInvoke.TryGetError());
                }
                break;
            case PInvoke.WM_APP_UNREGHOTKEY:
                {
                    var hotkey = Unpack(lParam);

                    if (PInvoke.UnregisterHotKey(hWnd, hotkey.Id))
                    {
                        _registeredHotkeys.Remove(lParam);
                        _logger.LogInfo($"Unregistered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
                        _logger.LogDebug(hotkey.ToString());
                    }
                    else _logger.LogError($"Cannot unregister the hotkey: {hotkey.Id}", PInvoke.TryGetError());
                }
                break;
        }

        return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
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