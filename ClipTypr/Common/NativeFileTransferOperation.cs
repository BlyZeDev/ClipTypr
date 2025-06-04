namespace ClipTypr.Common;

using System.Buffers.Text;
using System.Text;

public sealed class NativeFileTransferOperation : NativeTransferOperationBase, ITransferOperation
{
    private readonly string _tempZipPath;

    public TimeSpan EstimatedRuntime { get; }

    public NativeFileTransferOperation(ILogger logger, ConfigurationHandler configHandler, string tempZipPath) : base(logger, configHandler)
    {
        _tempZipPath = tempZipPath;
        EstimatedRuntime = EstimateFileTransferRuntime();
    }

    public void Send()
    {
        _logger.LogInfo("Starting to transfer the temporary .zip file");

        if (!File.Exists(_tempZipPath))
        {
            _logger.LogError("Temporay .zip file does not exist, aborting", null);
            return;
        }

        var foregroundHWnd = Native.GetForegroundWindow();
        if (foregroundHWnd == nint.Zero)
        {
            _logger.LogError("Could not fetch the current foreground window, aborting", Native.TryGetError());
            return;
        }

        int bytesRead;
        Span<byte> buffer = stackalloc byte[Base64.GetMaxEncodedToUtf8Length(BufferSize)];

        var chunkSize = 0u;
        Span<INPUT> inputToSend = stackalloc INPUT[(Encoding.UTF8.GetMaxCharCount(buffer.Length) + 3) * 2];
        using (var fileStream = new FileStream(_tempZipPath, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize))
        {
            FillInput("$b=@(", inputToSend, ref chunkSize);
            SendInputChunk(inputToSend, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
            if (!IsCorrectWindow(foregroundHWnd))
            {
                _logger.LogError("The focus of the windows was lost, aborting", null);
                return;
            }

            while ((bytesRead = fileStream.Read(buffer[..BufferSize])) > 0)
            {
                Base64.EncodeToUtf8InPlace(buffer, bytesRead, out var bytesWritten);

                FillInput($"\"{Encoding.UTF8.GetString(buffer[..bytesWritten])}\"{(fileStream.Position == fileStream.Length ? "" : ",")}", inputToSend, ref chunkSize);
                SendInputChunk(inputToSend, chunkSize);

                Thread.Sleep(GetTimeout(chunkSize));
                if (!IsCorrectWindow(foregroundHWnd))
                {
                    _logger.LogError("The focus of the windows was lost, aborting", null);
                    return;
                }
            }

            FillInput($");$fs=[System.IO.File]::OpenWrite((Join-Path (Get-Location).Path ", inputToSend, ref chunkSize);
            SendInputChunk(inputToSend, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
            if (!IsCorrectWindow(foregroundHWnd))
            {
                _logger.LogError("The focus of the windows was lost, aborting", null);
                return;
            }

            FillInput($"\"{nameof(ClipTypr)}-Transfer-{DateTime.UtcNow:yyyyMMddHHmmssff}Z.zip\"));", inputToSend, ref chunkSize);
            SendInputChunk(inputToSend, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
            if (!IsCorrectWindow(foregroundHWnd))
            {
                _logger.LogError("The focus of the windows was lost, aborting", null);
                return;
            }

            FillInput("$b | % { $bytes=[Convert]::FromBase64String($_);$fs.Write($bytes,0,$bytes.Length) }; $fs.Close()", inputToSend, ref chunkSize);
            SendInputChunk(inputToSend, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
        }
    }

    private TimeSpan EstimateFileTransferRuntime()
    {
        try
        {
            var bytesLength = (ulong)new FileInfo(_tempZipPath).Length;
            _logger.LogDebug($".zip file size: {bytesLength} Bytes");

            var encodedLength = 4 * ((bytesLength + 2) / 3);
            var totalChunks = encodedLength > ChunkSize ? encodedLength / ChunkSize : 1;

            return TimeSpan.FromMilliseconds(totalChunks * (ChunkSize * 2 * _transferTimeoutMultipliers[_configHandler.Current.TransferSecurity]));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not estimate transfer time", ex);
            return TimeSpan.Zero;
        }
    }
}