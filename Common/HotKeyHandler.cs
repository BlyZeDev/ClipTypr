namespace ClipTypr.Common;

using ClipTypr.NATIVE;
using System.Runtime.InteropServices;

public sealed class HotKeyHandler : IDisposable
{
    private const string WindowClassName = $"{nameof(ClipTypr)}HiddenWindow";

    private readonly Thread _messageThread;
    private readonly CancellationTokenSource _cts;

    private nint hWnd;

    public event EventHandler? Ready;
    public event EventHandler<HotKey>? HotKeyPressed;

    public unsafe HotKeyHandler()
    {
        _cts = new CancellationTokenSource();
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
                Logger.LogError("Could not register the class", Native.GetError());
                return;
            }

            var windowHandle = Native.CreateWindowEx(0, WindowClassName, "", 0, 0, 0, 0, 0, new nint(-3), nint.Zero, nint.Zero, nint.Zero);
            Interlocked.Exchange(ref hWnd, windowHandle);

            Ready?.Invoke(this, EventArgs.Empty);

            while (!_cts.IsCancellationRequested)
            {
                while (Native.GetMessage(out var msg, hWnd, 0, 0))
                {
                    Native.TranslateMessage(ref msg);
                    Native.DispatchMessage(ref msg);
                }
            }

            Native.DestroyWindow(hWnd);
            Interlocked.Exchange(ref hWnd, nint.Zero);
        });
    }

    public void RegisterHotKey(in HotKey hotkey)
    {
        if (hWnd == nint.Zero)
        {
            Logger.LogWarning("The message thread is not yet initialized");
            return;
        }

        Native.PostMessage(hWnd, Native.WM_APP_ADDHOTKEY, nint.Zero, Pack(hotkey));
    }

    public void UnregisterHotKey(in HotKey hotkey)
    {
        if (hWnd == nint.Zero)
        {
            Logger.LogWarning("The message thread is not yet initialized");
            return;
        }

        Native.PostMessage(hWnd, Native.WM_APP_REMOVEHOTKEY, nint.Zero, Pack(hotkey));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _messageThread.Join();
    }

    private nint WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Native.WM_HOTKEY: HotKeyPressed?.Invoke(this, Unpack(lParam)); break;
            case Native.WM_APP_ADDHOTKEY:
                {
                    var hotkey = Unpack(lParam);

                    if (Native.RegisterHotKey(hWnd, hotkey.Id, (uint)hotkey.Modifiers, (uint)hotkey.Key))
                    {
                        Logger.LogInfo($"Registered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
                        Logger.LogDebug(hotkey.ToString());
                    }
                    else Logger.LogError($"Cannot register the hotkey: {hotkey.Modifiers} - {hotkey.Key}", Native.GetError());
                }
                break;
            case Native.WM_APP_REMOVEHOTKEY:
                {
                    var hotkey = Unpack(lParam);

                    if (Native.UnregisterHotKey(hWnd, hotkey.Id))
                    {
                        Logger.LogInfo($"Unregistered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
                        Logger.LogDebug(hotkey.ToString());
                    }
                    else Logger.LogError($"Cannot unregister the hotkey: {hotkey.Id}", Native.GetError());
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