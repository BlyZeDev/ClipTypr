namespace StrokeMyKeys.Common;

using StrokeMyKeys.NATIVE;

public static class InputSimulator
{
    private const int ChunkSize = Util.StackSizeBytes / 4;

    public static unsafe void SendInput(in ReadOnlySpan<char> characters)
    {
        Span<INPUT> inputs = stackalloc INPUT[ChunkSize * 2];

        for (int offset = 0; offset < characters.Length; offset += ChunkSize)
        {
            var count = Math.Min(ChunkSize, characters.Length - offset);

            for (int i = 0; i < count; i++)
            {
                ref readonly var scanCode = ref characters[offset + i];

                ref var down = ref inputs[i * 2];
                ref var up = ref inputs[i * 2 + 1];

                down = new INPUT
                {
                    Type = Native.INPUT_KEYBOARD,
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
                    Type = Native.INPUT_KEYBOARD,
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

            Span<INPUT> sliced = inputs[..(count * 2)];
            fixed (INPUT* inputsPtr = sliced)
            {
                var result = Native.SendInput((uint)sliced.Length, inputsPtr, sizeof(INPUT));

                if (result == 0) Logger.LogError("Couldn't send inputs", Native.GetError());
                else if (result != sliced.Length) Logger.LogWarning($"{Math.Abs(sliced.Length - result)} inputs were lost", Native.GetError());
            }
        }
    }

    public static void SendFile(string filepath)
    {
        if (!File.Exists(filepath))
        {
            Logger.LogWarning("The file does not exist");
            return;
        }

        //TODO Get this shit to work smh
    }
}