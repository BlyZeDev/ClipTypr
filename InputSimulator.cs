namespace StrokeMyKeys;

public readonly ref struct InputSimulator
{
    private readonly ReadOnlySpan<char> _characters;

    public InputSimulator(in ReadOnlySpan<char> characters) => _characters = characters;

    public unsafe void SendInput()
    {
        Span<INPUT> inputs = stackalloc INPUT[_characters.Length * 2];
        for (int i = 0; i < _characters.Length; i++)
        {
            ref readonly var scanCode = ref _characters[i];

            ref var down = ref inputs[i * 2];
            ref var up = ref inputs[i * 2 + 1];

            down = new INPUT
            {
                Type = 1,
                Union = new INPUT_UNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        KeyCode = 0,
                        Scan = scanCode,
                        Flags = Native.KEYEVENTF_UNICODE,
                        Time = 0,
                        ExtraInfo = nint.Zero
                    }
                }
            };

            up = new INPUT
            {
                Type = 1,
                Union = new INPUT_UNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        KeyCode = 0,
                        Scan = scanCode,
                        Flags = Native.KEYEVENTF_KEYUP | Native.KEYEVENTF_UNICODE,
                        Time = 0,
                        ExtraInfo = nint.Zero
                    }
                }
            };

            if ((scanCode & 0xFF00) == 0xE000)
            {
                down.Union.Keyboard.Flags |= Native.KEYEVENTF_EXTENDEDKEY;
                up.Union.Keyboard.Flags |= Native.KEYEVENTF_EXTENDEDKEY;
            }
        }

        fixed (INPUT* inputsPtr = inputs)
        {
            var result = Native.SendInput((uint)inputs.Length, inputsPtr, sizeof(INPUT));
            if (result == 0)
            {
                Logger.LogError("Couldn't send inputs", Native.GetError());
                return;
            }
        }
    }
}