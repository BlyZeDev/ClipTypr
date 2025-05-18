namespace ClipTypr.Common;

using ClipTypr.NATIVE;
using System.Buffers.Text;
using System.IO.Compression;
using System.Text;

public static class InputSimulator
{
    private const int ChunkSize = Util.StackSizeBytes / 4;

    public static unsafe void SendInput(in ReadOnlySpan<char> characters)
    {
        Logger.LogDebug($"Sending {characters.Length} characters");

        if (characters.Length <= ChunkSize)
        {
            Span<INPUT> inputs = stackalloc INPUT[characters.Length * 2];

            for (int i = 0; i < characters.Length; i++)
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

            fixed (INPUT* inputsPtr = inputs)
            {
                var inputSent = Native.SendInput((uint)inputs.Length, inputsPtr, sizeof(INPUT));

                if (inputSent == 0) Logger.LogError("Couldn't send inputs", Native.GetError());
                else if (inputSent != inputs.Length) Logger.LogWarning($"{Math.Abs(inputs.Length - inputSent)} inputs were lost", Native.GetError());
                else Logger.LogDebug($"Successfully sent {inputSent} inputs");
            }
        }
        else
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
                    var inputSent = Native.SendInput((uint)sliced.Length, inputsPtr, sizeof(INPUT));

                    if (inputSent == 0) Logger.LogError("Couldn't send inputs", Native.GetError());
                    else if (inputSent != sliced.Length) Logger.LogWarning($"{Math.Abs(sliced.Length - inputSent)} inputs were lost", Native.GetError());
                    else Logger.LogDebug($"Successfully sent {inputSent} inputs");

                    Thread.Sleep(50);
                }
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
        
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"{Guid.CreateVersion7()}.zip");
        using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(Path.GetFileName(filepath), CompressionLevel.SmallestSize);

                using (var entryStream = entry.Open())
                {
                    using (var fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }
        }
        
        const int BufferSize = Util.StackSizeBytes / 4;
        
        int bytesRead;
        Span<byte> buffer = stackalloc byte[BufferSize];
        Span<byte> utf8Buffer = stackalloc byte[Base64.GetMaxEncodedToUtf8Length(buffer.Length)];
        using (var fileStream = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize))
        {
            SendInput("$b=@(");
            Thread.Sleep(25);

            while ((bytesRead = fileStream.Read(buffer)) > 0)
            {
                Base64.EncodeToUtf8(buffer, utf8Buffer, out _, out var bytesWritten);

                var base64 = $"\"{Encoding.UTF8.GetString(utf8Buffer[..bytesWritten])}\"{(fileStream.Position == fileStream.Length ? "" : ",")}";
                SendInput(base64);

                Thread.Sleep(base64.Length);
            }

            SendInput($");$fs=[System.IO.File]::OpenWrite((Join-Path (Get-Location).Path ");
            Thread.Sleep(250);
            SendInput($"\"{Path.GetFileNameWithoutExtension(filepath)}.zip\"));");
            Thread.Sleep(250);
            SendInput("$b | % { $bytes=[Convert]::FromBase64String($_);");
            Thread.Sleep(500);
            SendInput("$fs.Write($bytes,0,$bytes.Length) }; $fs.Close()");
            Thread.Sleep(1000);
        }

        File.Delete(tempZipPath);
    }
}