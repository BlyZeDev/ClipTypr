namespace StrokeMyKeys.Common;

using StrokeMyKeys.NATIVE;

public static class InputSimulator
{
    private const int CooldownMs = 1000;
    private const int MaxChunkSize = 2048;

    public static unsafe void SendInput(in ReadOnlySpan<char> characters)
    {
        var size = characters.Length * 2;

        Span<INPUT> inputs = Util.AllowStack<INPUT>(size) ? stackalloc INPUT[size] : new INPUT[size];
        for (var i = 0; i < characters.Length; i++)
        {
            ref readonly var scanCode = ref characters[i];

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

        for (var i = 0; i < inputs.Length; i += MaxChunkSize)
        {
            var chunk = inputs.Slice(i, Math.Min(MaxChunkSize, inputs.Length - i));

            fixed (INPUT* inputsPtr = chunk)
            {
                var result = Native.SendInput((uint)chunk.Length, inputsPtr, sizeof(INPUT));

                if (result == 0) Logger.LogError("Couldn't send inputs", Native.GetError());
                else if (result != chunk.Length) Logger.LogWarning($"{Math.Abs(chunk.Length - result)} inputs were lost", Native.GetError());
            }

            Thread.Sleep(CooldownMs);
        }
    }

    public static unsafe void SendFile(string filepath)
    {
        if (!File.Exists(filepath))
        {
            Logger.LogWarning("The file does not exist");
            return;
        }
        
        Span<byte> buffer = stackalloc byte[Util.StackSizeBytes / 2];
        var bytesRead = 0;
        using (var fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var commandBuilder = new SpanBuilder("$b+=[byte[]]@(");

            while ((bytesRead = fileStream.Read(buffer)) > 0)
            {
                var slice = buffer[..bytesRead];

                for (var i = 0; i < slice.Length; i++)
                {
                    commandBuilder.Append("0x");

                    ref readonly var b = ref slice[i];
                    commandBuilder.Append(GetHexChar(b >> 4));
                    commandBuilder.Append(GetHexChar(b & 0x0F));

                    commandBuilder.Append(',');
                }
            }

            commandBuilder.Length--;
            commandBuilder.Append(");");

            SendInput(commandBuilder.AsSpan());
        }

        SendInput($"[IO.File]::WriteAllBytes((Join-Path (Get-Location).Path \"{Path.GetFileName(filepath)}\"), $b)");
    }

    private static char GetHexChar(int value) => (char)(value < 10 ? '0' + value : 'A' + (value - 10));
}