namespace ClipTypr.Services;

public sealed class HotKeyHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly NativeMessageHandler _messageHandler;

    private readonly HashSet<nint> _registeredHotkeys;

    public event EventHandler<HotKey>? HotKeyPressed;

    public HotKeyHandler(ILogger logger, NativeMessageHandler messageHandler)
    {
        _logger = logger;
        _messageHandler = messageHandler;
        _messageHandler.WndProc += WndProcFunc;

        _registeredHotkeys = [];
    }

    public void RegisterHotKey(in HotKey hotkey) => _messageHandler.Post(PInvoke.WM_APP_REGHOTKEY, nint.Zero, Pack(hotkey));

    public void UnregisterHotKey(in HotKey hotkey) => _messageHandler.Post(PInvoke.WM_APP_UNREGHOTKEY, nint.Zero, Pack(hotkey));

    public void Dispose()
    {
        foreach (var hotkey in _registeredHotkeys)
        {
            _messageHandler.Post(PInvoke.WM_APP_UNREGHOTKEY, nint.Zero, hotkey);
        }

        _messageHandler.WndProc -= WndProcFunc;

        GC.SuppressFinalize(this);
    }

    private void WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
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