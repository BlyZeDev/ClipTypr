namespace ClipTypr.Services;

using System.Buffers.Text;
using System.IO.Compression;
using System.Text;

public sealed class InputSimulator : IDisposable
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

    private readonly ILogger _logger;
    private readonly ConfigurationHandler _configHandler;

    public InputSimulator(ILogger logger, ConfigurationHandler configHandler)
    {
        _logger = logger;
        _configHandler = configHandler;
    }

    public TimeSpan PrepareFileTransfer(IEnumerable<string> files)
    {
        var tempZipPath = GetTempZipPath();

        using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var path in files)
                {
                    if (File.Exists(path))
                    {
                        var entry = archive.CreateEntry(Path.GetFileName(path), CompressionLevel.SmallestSize);

                        using (var entryStream = entry.Open())
                        {
                            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                    }
                    else if (Directory.Exists(path))
                    {
                        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        {
                            var entry = archive.CreateEntry(Path.Combine(Path.GetFileName(path), Path.GetRelativePath(path, file).Replace(Path.DirectorySeparatorChar, '/')), CompressionLevel.SmallestSize);

                            using (var entryStream = entry.Open())
                            {
                                using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                            }
                        }
                    }
                    else _logger.LogWarning($"The file or directory does not exist: {path}");
                }
            }
        }

        _logger.LogInfo("Temporary .zip file created, do not touch");
        _logger.LogDebug(tempZipPath);

        return EstimateFileTransferRuntime();
    }

    public void SendFiles()
    {
        if (!File.Exists(GetTempZipPath()))
        {
            _logger.LogWarning("Temporay .zip file does not exist, aborting");
            return;
        }

        const int BufferSize = Util.StackSizeBytes / 4;

        int bytesRead;
        Span<byte> buffer = stackalloc byte[BufferSize];
        Span<byte> utf8Buffer = stackalloc byte[Base64.GetMaxEncodedToUtf8Length(buffer.Length)];
        using (var fileStream = new FileStream(GetTempZipPath(), FileMode.Open, FileAccess.Read, FileShare.None, BufferSize))
        {
            SendText("$b=@(");

            while ((bytesRead = fileStream.Read(buffer)) > 0)
            {
                Base64.EncodeToUtf8(buffer, utf8Buffer, out _, out var bytesWritten);

                SendText($"\"{Encoding.UTF8.GetString(utf8Buffer[..bytesWritten])}\"{(fileStream.Position == fileStream.Length ? "" : ",")}");
            }

            SendText($");$fs=[System.IO.File]::OpenWrite((Join-Path (Get-Location).Path \"{nameof(ClipTypr)}-Transfer-{DateTime.UtcNow:yyyyMMddHHmmssff}Z.zip\"));$b | % {{ $bytes=[Convert]::FromBase64String($_);$fs.Write($bytes,0,$bytes.Length) }}; $fs.Close()");
            Thread.Sleep(10000);
        }
    }

    public unsafe void SendText(in ReadOnlySpan<char> characters)
    {
        _logger.LogDebug($"Sending {characters.Length} characters");

        var multiplier = _transferTimeoutMultipliers[_configHandler.Current.TransferSecurity];

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

                if (inputSent == 0) _logger.LogError("Couldn't send inputs", Native.GetError());
                else if (inputSent != count) _logger.LogWarning($"{Math.Abs(count - inputSent)} inputs were lost", Native.GetError());
                else _logger.LogDebug($"Successfully sent {inputSent} inputs");

                Thread.Sleep((int)(inputSent * multiplier));
            }

            offset += count / 2;
        }
    }

    public void Dispose()
    {
        var path = GetTempZipPath();
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInfo("Temporary .zip file cleaned up");
        }
    }

    private TimeSpan EstimateFileTransferRuntime()
    {
        try
        {
            var bytesLength = (ulong)new FileInfo(GetTempZipPath()).Length;

            var encodedLength = 4 * ((bytesLength + 2) / 3);
            var totalChunks = encodedLength / ChunkSize;

            return TimeSpan.FromMilliseconds(totalChunks * (ChunkSize * 2 * _transferTimeoutMultipliers[_configHandler.Current.TransferSecurity]));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not estimate transfer time", ex);
            return TimeSpan.Zero;
        }
    }

    private static void FillInputSpan(in ReadOnlySpan<char> characters, in Span<INPUT> inputs)
    {
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
                        Scan = scanCode,
                        Flags = Native.KEYEVENTF_KEYUP | Native.KEYEVENTF_UNICODE,
                        Time = 0,
                        ExtraInfo = Native.GetMessageExtraInfo()
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

    private static string GetTempZipPath() => Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-TempFileTransfer.zip");
}