namespace ClipTypr.Common;

using System.Diagnostics;
using System.Runtime.InteropServices;

public sealed class KeyboardTranslator : IDisposable
{
    private readonly ILogger _logger;

    private nint hookId;
    private Native.KeyboardProc? keyboardProc;

    public bool IsTranslating => hookId != nint.Zero && keyboardProc is not null;

    public KeyboardTranslator(ILogger logger) => _logger = logger;

    public void Start()
    {
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
        if (hookId != nint.Zero)
        {
            Native.UnhookWindowsHookEx(hookId);

            hookId = nint.Zero;
            keyboardProc = null;

            _logger.LogInfo("The keyboard translation is stopped");
        }
    }

    public void Dispose() => Stop();

    private unsafe nint KeyboardProcFunc(int nCode, nint wParam, nint lParam)
    {
        if (wParam != Native.WM_KEYDOWN || nCode < 0) return Native.CallNextHookEx(hookId, nCode, wParam, lParam);

        var keyboardInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        if ((keyboardInfo.flags & Native.LLKHF_REPEAT) != 0) return Native.CallNextHookEx(hookId, nCode, wParam, lParam);

        Span<byte> keyboardState = stackalloc byte[256];
        fixed (byte* keyboardStatePtr = keyboardState)
        {
            if (!Native.GetKeyboardState(keyboardStatePtr))
            {
                _logger.LogError("Could not get the current keyboard state", Native.TryGetError());
                return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            var layout = Native.GetKeyboardLayout(0);
            if (layout == nint.Zero)
            {
                _logger.LogError("Could not get the keyboard layout", Native.TryGetError());
                return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            Span<char> unicodeData = stackalloc char[5];
            fixed (char* unicodeDataPtr = unicodeData)
            {
                var result = Native.ToUnicodeEx(keyboardInfo.vkCode, keyboardInfo.scanCode, keyboardStatePtr, unicodeDataPtr, unicodeData.Length, 0, layout);

                if (result > 0)
                {
                    SendCharacter(unicodeData[0]);
                    return 1;
                }
                else
                {
                    _logger.LogError("Could not convert the key to unicode", Native.TryGetError());
                    return Native.CallNextHookEx(hookId, nCode, wParam, lParam);
                }
            }
        }
    }

    private unsafe void SendCharacter(char character)
    {
        Span<INPUT> input = stackalloc INPUT[2];

        ref var down = ref input[0];
        ref var up = ref input[1];

        down = new INPUT
        {
            Type = Native.INPUT_KEYBOARD,
            Union = new INPUT_UNION
            {
                Keyboard = new KEYBDINPUT
                {
                    KeyCode = 0,
                    Scan = character,
                    Flags = Native.KEYEVENTF_UNICODE,
                    Time = 0,
                    ExtraInfo = Native.GetMessageExtraInfo()
                }
            }
        };

        up = new INPUT
        {
            Type = Native.INPUT_KEYBOARD,
            Union = new INPUT_UNION
            {
                Keyboard = new KEYBDINPUT
                {
                    KeyCode = 0,
                    Scan = character,
                    Flags = Native.KEYEVENTF_KEYUP | Native.KEYEVENTF_UNICODE,
                    Time = 0,
                    ExtraInfo = Native.GetMessageExtraInfo()
                }
            }
        };

        if ((character & 0xFF00) == 0xE000)
        {
            down.Union.Keyboard.Flags |= Native.KEYEVENTF_EXTENDEDKEY;
            up.Union.Keyboard.Flags |= Native.KEYEVENTF_EXTENDEDKEY;
        }

        fixed (INPUT* inputPtr = input)
        {
            var inputSent = Native.SendInput((uint)input.Length, inputPtr, sizeof(INPUT));

            if (inputSent == 0) _logger.LogError("Couldn't send inputs", Native.TryGetError());
            else if (inputSent != input.Length) _logger.LogWarning($"{Math.Abs(input.Length - inputSent)} inputs were lost", Native.TryGetError());
            else _logger.LogDebug($"Successfully sent {inputSent} inputs");
        }
    }
}