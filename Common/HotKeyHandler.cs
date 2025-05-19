namespace ClipTypr.Common;

using ClipTypr.NATIVE;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static ClipTypr.Common.HotKeyHandler;

public sealed class HotKeyHandler : IDisposable
{
    private const string WindowClassName = $"{nameof(ClipTypr)}HiddenWindow";

    private readonly Thread _messageThread;
    private readonly CancellationTokenSource _cts;
    private readonly BlockingCollection<Action<nint>> _messages;

    public event EventHandler<HotKey>? HotKeyPressed;

    public HotKeyHandler()
    {
        _cts = new CancellationTokenSource();
        _messages = [];
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

            var hWnd = Native.CreateWindowEx(0, WindowClassName, "", 0, 0, 0, 0, 0, new nint(-3), nint.Zero, nint.Zero, nint.Zero);

            while (!_cts.IsCancellationRequested)
            {
                while (_messages.TryTake(out var action))
                {
                    action(hWnd);
                }

                while (Native.PeekMessage(out var msg, hWnd, 0, 0, Native.PM_REMOVE))
                {
                    Native.TranslateMessage(ref msg);
                    Native.DispatchMessage(ref msg);
                }
            }
        });
        _messageThread.Start();
    }

    public void RegisterHotKey(HotKey hotkey)
    {
        _messages.Add(hWnd =>
        {
            if (!Native.RegisterHotKey(hWnd, hotkey.Id, (uint)hotkey.Modifiers, (uint)hotkey.Key))
            {
                Logger.LogError($"Cannot register the hotkey: {hotkey.Modifiers} - {hotkey.Key}", Native.GetError());
                return;
            }

            Logger.LogInfo($"Registered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
            Logger.LogDebug(hotkey.ToString());
        });
    }

    public void UnregisterHotKey(HotKey hotkey)
    {
        _messages.Add(hWnd =>
        {
            if (!Native.UnregisterHotKey(hWnd, hotkey.Id))
            {
                Logger.LogError($"Cannot unregister the hotkey: {hotkey.Id}", Native.GetError());
                return;
            }

            Logger.LogInfo($"Unregistered the hotkey: {hotkey.Modifiers} - {hotkey.Key}");
            Logger.LogDebug(hotkey.ToString());
        });
    }

    public void Dispose()
    {
        Logger.LogDebug("Unregistered all hotkeys while cleaning up");

        _cts.Cancel();
        _cts.Dispose();
        _messageThread.Join();
    }

    private nint WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == Native.WM_HOTKEY)
        {
            HotKeyPressed?.Invoke(this, new HotKey
            {
                Modifiers = (ConsoleModifiers)(uint)(lParam.ToInt64() & 0xFFFF),
                Key = (ConsoleKey)(uint)((lParam.ToInt64() >> 16) & 0xFFFF)
            });
        }

        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}