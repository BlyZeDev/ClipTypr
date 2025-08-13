namespace ClipTypr.Services;

using System.Runtime.InteropServices;

public sealed class NativeMessageHandler : IDisposable
{
    private const string WindowClassName = $"{nameof(ClipTypr)}HiddenWindow";

    private readonly ILogger _logger;

    private readonly ManualResetEventSlim _waitHandle;
    private readonly Thread _messageThread;

    private nint hWnd;

    public event Action<nint, uint, nint, nint>? WndProc;

    public nint HWnd
    {
        get
        {
            WaitForReady();
            return hWnd;
        }
    }

    public NativeMessageHandler(ILogger logger)
    {
        _logger = logger;
        _waitHandle = new ManualResetEventSlim();
        _messageThread = new Thread(() =>
        {
            _logger.LogDebug("Starting the message thread");

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

            Interlocked.Exchange(ref hWnd, PInvoke.CreateWindowEx(0, WindowClassName, "", 0, 0, 0, 0, 0, new nint(-3), nint.Zero, nint.Zero, nint.Zero));

            _waitHandle.Set();
            while (PInvoke.GetMessage(out var msg, hWnd, 0, 0))
            {
                if (msg.message == PInvoke.WM_CLOSE) break;

                PInvoke.TranslateMessage(ref msg);
                PInvoke.DispatchMessage(ref msg);
            }

            PInvoke.DestroyWindow(hWnd);
            GC.KeepAlive(wndProc);

            _logger.LogDebug("Message thread is finished");
        });
        _messageThread.Start();
    }

    public void Post(uint msg, nint wParam, nint lParam)
    {
        WaitForReady();
        PInvoke.PostMessage(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        _waitHandle.Dispose();

        PInvoke.PostMessage(hWnd, PInvoke.WM_CLOSE, 0, 0);

        if (_messageThread.IsAlive) _messageThread.Join();
        
        GC.SuppressFinalize(this);
    }

    private void WaitForReady()
    {
        if (_waitHandle.IsSet) return;

        _logger.LogDebug("Waiting for ready");

        var wasSignaled = _waitHandle.Wait(TimeSpan.FromSeconds(5));
        if (!wasSignaled) throw new TimeoutException($"{nameof(NativeMessageHandler)} timed out waiting for ready");
    }

    private nint WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        WndProc?.Invoke(hWnd, msg, wParam, lParam);
        return PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}