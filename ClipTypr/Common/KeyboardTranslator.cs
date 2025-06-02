namespace ClipTypr.Common;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public sealed class KeyboardTranslator : NativeTransferOperationBase, IDisposable
{
    private const int KeyboardStateLength = 256;

    private nint hookId;
    private Native.KeyboardProc? keyboardProc;

    private readonly byte[] lastKeyboardState;
    private uint lastVkCode;
    private uint lastScanCode;
    private bool lastKeyDead;

    public bool IsTranslating => hookId != nint.Zero && keyboardProc is not null;

    public KeyboardTranslator(ILogger logger, ConfigurationHandler configHandler) : base(logger, configHandler)
    {
        lastVkCode = 0;
        lastScanCode = 0;
        lastKeyboardState = new byte[KeyboardStateLength];
        lastKeyDead = false;
    }

    public void Start()
    {
        if (IsTranslating) return;

        Reset();

        keyboardProc = new Native.KeyboardProc(KeyboardProcFunc);

        using (var process = Process.GetCurrentProcess())
        {
            using (var module = process.MainModule)
            {
                if (module is null)
                {
                    _logger.LogError("Could not find the main module of the process", null);
                    return;
                }

                hookId = Native.SetWindowsHookEx(
                    Native.WH_KEYBOARD_LL,
                    Marshal.GetFunctionPointerForDelegate(keyboardProc),
                    Native.GetModuleHandle(module.ModuleName),
                    0);

                if (hookId == nint.Zero)
                {
                    _logger.LogError("Could not set up a keyboard hook", Native.TryGetError());
                }
            }
        }

        _logger.LogInfo("The keyboard translation is running");
    }

    public void Stop()
    {
        if (!IsTranslating) return;

        Native.UnhookWindowsHookEx(hookId);

        Reset();

        _logger.LogInfo("The keyboard translation has stopped");
    }

    private void Reset()
    {
        hookId = nint.Zero;
        keyboardProc = null;

        lastVkCode = 0;
        lastScanCode = 0;
        Array.Clear(lastKeyboardState);
        lastKeyDead = false;
    }

    public void Dispose() => Stop();

    private unsafe nint KeyboardProcFunc(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0) return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
        if (wParam is not Native.WM_KEYDOWN or Native.WM_KEYUP or Native.WM_SYSKEYDOWN or Native.WM_SYSKEYUP) return Native.CallNextHookEx(hookId, nCode, wParam, lParam);

        var isKeyDown = wParam is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;

        var keyboardInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

        Span<byte> keyboardState = stackalloc byte[KeyboardStateLength];
        fixed (byte* keyboardStatePtr = keyboardState)
        {
            var foregroundHWnd = Native.GetForegroundWindow();
            var foregroundThreadId = Native.GetWindowThreadProcessId(foregroundHWnd, out var foregroundProcessId);

            var currentThreadId = Native.GetCurrentThreadId();

            var couldAttach = Native.AttachThreadInput(currentThreadId, foregroundThreadId, true);

            if (!Native.GetKeyboardState(keyboardStatePtr))
            {
                _logger.LogError("Could not get the current keyboard state", Native.TryGetError());
                return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            if (couldAttach) Native.AttachThreadInput(currentThreadId, foregroundThreadId, false);

            var layout = Native.GetKeyboardLayout(currentThreadId);
            if (layout == nint.Zero)
            {
                _logger.LogError("Could not get the keyboard layout", Native.TryGetError());
                return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            if (!isKeyDown) return Native.CallNextHookEx(hookId, nCode, wParam, lParam);

            var isHandled = false;
            var isDead = false;
            Span<char> unicodeData = stackalloc char[5];
            fixed (char* unicodeDataPtr = unicodeData)
            {
                var result = Native.ToUnicodeEx(keyboardInfo.vkCode, keyboardInfo.scanCode, keyboardStatePtr, unicodeDataPtr, unicodeData.Length, 0, layout);

                switch (result)
                {
                    case -1:
                        fixed (byte* nullKeyboardStatePtr = stackalloc byte[KeyboardStateLength])
                        {
                            while (result < 0)
                            {
                                result = Native.ToUnicodeEx(keyboardInfo.vkCode, keyboardInfo.scanCode, nullKeyboardStatePtr, unicodeDataPtr, unicodeData.Length, 0, layout);
                                Unsafe.InitBlock(nullKeyboardStatePtr, 0, KeyboardStateLength);
                            }
                        }

                        isDead = true;
                        isHandled = true;
                        break;
                    case 0: break;
                    case 1:
                        {
                            Span<INPUT> input = stackalloc INPUT[2];
                            var chunkSize = 0u;

                            FillInputSpan(unicodeData[..1], input, ref chunkSize);
                            SendInputChunk(input, chunkSize);

                            isHandled = true;
                        }
                        break;
                    case > 1:
                        {
                            Span<INPUT> input = stackalloc INPUT[4];
                            var chunkSize = 0u;

                            FillInputSpan(unicodeData[..2], input, ref chunkSize);
                            SendInputChunk(input, chunkSize);

                            isHandled = true;
                        }
                        break;
                }
            }

            if (lastVkCode != 0 && lastKeyDead)
            {
                fixed (byte* lastKeyboardStatePtr = lastKeyboardState)
                {
                    fixed (char* unicodeDataPtr = unicodeData)
                    {
                         _ = Native.ToUnicodeEx(lastVkCode, lastScanCode, lastKeyboardStatePtr, unicodeDataPtr, unicodeData.Length, 0, layout);
                        lastVkCode = 0;
                    }
                }

                isHandled = true;
            }

            lastVkCode = keyboardInfo.vkCode;
            lastScanCode = keyboardInfo.scanCode;
            keyboardState.CopyTo(lastKeyboardState.AsSpan());
            lastKeyDead = isDead;

            return isHandled ? 1 : Native.CallNextHookEx(hookId, nCode, wParam, lParam);
        }
    }
}