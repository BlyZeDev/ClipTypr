namespace ClipTypr.Common;

using ClipTypr.NATIVE;
using System.Buffers.Text;
using System.IO.Compression;
using System.Text;

public sealed class InputSimulator
{
    private const int ChunkSize = Util.StackSizeBytes / 4;
    private static readonly Dictionary<TransferSecurity, double> _transferTimeoutMultipliers = new Dictionary<TransferSecurity, double>
    {
        { TransferSecurity.VeryUnsafe, 0.25 },
        { TransferSecurity.Unsafe, 0.5 },
        { TransferSecurity.Average, 1 },
        { TransferSecurity.Safe, 2 },
        { TransferSecurity.VerySafe, 4 },
        { TransferSecurity.Guaranteed, 8 }
    };

    public TimeSpan EstimatedTransferTime { get; }

    private InputSimulator(TransferSecurity transferSecurity)
        => EstimatedTransferTime = EstimateFileTransferRuntime(transferSecurity);

    public void SendFiles(TransferSecurity transferSecurity)
    {
        if (!File.Exists(GetTempZipPath()))
        {
            Logger.LogWarning("Temporay .zip file does not exist anymore, aborting");
            return;
        }

        const int BufferSize = Util.StackSizeBytes / 4;

        int bytesRead;
        Span<byte> buffer = stackalloc byte[BufferSize];
        Span<byte> utf8Buffer = stackalloc byte[Base64.GetMaxEncodedToUtf8Length(buffer.Length)];
        using (var fileStream = new FileStream(GetTempZipPath(), FileMode.Open, FileAccess.Read, FileShare.None, BufferSize))
        {
            SendText("$b=@(", transferSecurity);

            while ((bytesRead = fileStream.Read(buffer)) > 0)
            {
                Base64.EncodeToUtf8(buffer, utf8Buffer, out _, out var bytesWritten);

                SendText($"\"{Encoding.UTF8.GetString(utf8Buffer[..bytesWritten])}\"{(fileStream.Position == fileStream.Length ? "" : ",")}", transferSecurity);
            }

            SendText($");$fs=[System.IO.File]::OpenWrite((Join-Path (Get-Location).Path \"{nameof(ClipTypr)}-Transfer-{DateTime.UtcNow:yyyyMMddHHmmssff}Z.zip\"));$b | % {{ $bytes=[Convert]::FromBase64String($_);$fs.Write($bytes,0,$bytes.Length) }}; $fs.Close()", transferSecurity);
            Thread.Sleep(10000);
        }
    }

    public static InputSimulator PrepareFileTransfer(IEnumerable<string> files, TransferSecurity transferSecurity)
    {
        var tempZipPath = GetTempZipPath();

        using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var path in files)
                {
                    if (!File.Exists(path))
                    {
                        Logger.LogWarning($"The file does not exist: {path}");
                        continue;
                    }

                    var entry = archive.CreateEntry(Path.GetFileName(path), CompressionLevel.SmallestSize);

                    using (var entryStream = entry.Open())
                    {
                        using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                        {
                            fileStream.CopyTo(entryStream);
                        }
                    }
                }                
            }
        }

        Logger.LogInfo("Temporary .zip file created, do not touch");
        Logger.LogDebug(tempZipPath);

        return new InputSimulator(transferSecurity);
    }

    public static unsafe void SendText(in ReadOnlySpan<char> characters, TransferSecurity transferSecurity)
    {
        Logger.LogDebug($"Sending {characters.Length} characters");

        var multiplier = _transferTimeoutMultipliers[transferSecurity];

        var total = characters.Length;
        Span<INPUT> inputBuffer = stackalloc INPUT[Math.Min(total, ChunkSize) * 2];

        int count;
        var offset = 0;
        while (offset < total)
        {
            count = Math.Min(ChunkSize, total - offset);
            FillInputSpan(characters.Slice(offset, count), inputBuffer[..(count * 2)]);
            count *= 2;

            fixed (INPUT* inputBufferPtr = inputBuffer)
            {
                var inputSent = Native.SendInput((uint)count, inputBufferPtr, sizeof(INPUT));

                if (inputSent == 0) Logger.LogError("Couldn't send inputs", Native.GetError());
                else if (inputSent != count) Logger.LogWarning($"{Math.Abs(count - inputSent)} inputs were lost", Native.GetError());
                else Logger.LogDebug($"Successfully sent {inputSent} inputs");

                Thread.Sleep((int)(inputSent * multiplier));
            }

            offset += count / 2;
        }
    }

    private static void FillInputSpan(in ReadOnlySpan<char> characters, in Span<INPUT> inputs)
    {
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
    }

    private static TimeSpan EstimateFileTransferRuntime(TransferSecurity transferSecurity)
    {
        try
        {
            var bytesLength = (ulong)new FileInfo(GetTempZipPath()).Length;

            var encodedLength = 4 * ((bytesLength + 2) / 3);
            var totalChunks = encodedLength / ChunkSize;

            return TimeSpan.FromMilliseconds(totalChunks * (ChunkSize * 2 * _transferTimeoutMultipliers[transferSecurity]));
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Could not estimate transfer time", ex);
            return TimeSpan.Zero;
        }
    }

    private static string GetTempZipPath() => Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-TempFileTransfer.zip");
}