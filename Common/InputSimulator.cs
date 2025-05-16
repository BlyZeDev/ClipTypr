namespace StrokeMyKeys.Common;

using StrokeMyKeys.NATIVE;
using System.Buffers;
using System.Buffers.Text;
using System.Text;

public static class InputSimulator
{
    private const int CooldownMs = 50;
    private const int MaxChunkSize = 1024;

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

        if (inputs.Length < MaxChunkSize)
        {
            fixed (INPUT* inputsPtr = inputs)
            {
                var result = Native.SendInput((uint)inputs.Length, inputsPtr, sizeof(INPUT));

                if (result == 0) Logger.LogError("Couldn't send inputs", Native.GetError());
                else if (result != inputs.Length) Logger.LogWarning($"{Math.Abs(inputs.Length - result)} inputs were lost", Native.GetError());
            }
        }
        else
        {
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
    }

    public static void SendFile(string filepath)
    {
        if (!File.Exists(filepath))
        {
            Logger.LogWarning("The file does not exist");
            return;
        }
        
        Span<byte> buffer = stackalloc byte[Util.StackSizeBytes / 4];
        Span<byte> base64Buffer = stackalloc byte[4 * ((buffer.Length + 2) / 3)];

        SendInput("$b=\"\";");

        var bytesRead = 0;
        using (var fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            while ((bytesRead = fileStream.Read(buffer)) > 0)
            {
                var slice = buffer[..bytesRead];

                if (Base64.EncodeToUtf8(slice, base64Buffer, out var consumed, out var written) is not OperationStatus.Done)
                {
                    Logger.LogError("Could not encode the text to Base64, aborting", null);
                    return;
                }

                SendInput($"$b+=\"{Encoding.UTF8.GetString(base64Buffer)}\";");
            }
        }

        SendInput($"[IO.File]::WriteAllBytes((Join-Path (Get-Location).Path \"{Path.GetFileName(filepath)}\"),[Convert]::FromBase64String($b))");
    }

    private static unsafe void SendInputChunk()
    {

    }

    private static unsafe void SendInput(INPUT* inputPtr, uint inputLength)
    {
        fixed (INPUT* inputsPtr = chunk)
        {
            var result = Native.SendInput((uint)chunk.Length, inputsPtr, sizeof(INPUT));

            if (result == 0) Logger.LogError("Couldn't send inputs", Native.GetError());
            else if (result != chunk.Length) Logger.LogWarning($"{Math.Abs(chunk.Length - result)} inputs were lost", Native.GetError());
        }
    }
}