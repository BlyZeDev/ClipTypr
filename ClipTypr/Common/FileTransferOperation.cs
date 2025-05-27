namespace ClipTypr.Common;

using System.Buffers;
using System.Buffers.Text;
using System.Text;

public sealed class FileTransferOperation : TransferOperationBase
{
    private const int BufferSize = ChunkSize / 2;

    private readonly string _tempZipPath;

    public TimeSpan EstimatedRuntime { get; }

    public FileTransferOperation(ILogger logger, ConfigurationHandler configHandler, string tempZipPath)
        : base(logger, configHandler)
    {
        _tempZipPath = tempZipPath;
        EstimatedRuntime = EstimateFileTransferRuntime();
    }

    public override void Send()
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
            _logger.LogError("Could not fetch the current foreground window, aborting", Native.GetError());
            return;
        }

        int bytesRead;
        Span<byte> buffer = stackalloc byte[Base64.GetMaxEncodedToUtf8Length(BufferSize)];

        var chunkSize = 0u;
        Span<INPUT> inputToSend = stackalloc INPUT[(Encoding.UTF8.GetMaxCharCount(buffer.Length) + 3) * 2];
        using (var fileStream = new FileStream(_tempZipPath, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize))
        {
            FillInputSpan("$b=@(", inputToSend, ref chunkSize);
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

                FillInputSpan($"\"{Encoding.UTF8.GetString(buffer[..bytesWritten])}\"{(fileStream.Position == fileStream.Length ? "" : ",")}", inputToSend, ref chunkSize);
                SendInputChunk(inputToSend, chunkSize);

                Thread.Sleep(GetTimeout(chunkSize));
                if (!IsCorrectWindow(foregroundHWnd))
                {
                    _logger.LogError("The focus of the windows was lost, aborting", null);
                    return;
                }
            }

            FillInputSpan($");$fs=[System.IO.File]::OpenWrite((Join-Path (Get-Location).Path ", inputToSend, ref chunkSize);
            SendInputChunk(inputToSend, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
            if (!IsCorrectWindow(foregroundHWnd))
            {
                _logger.LogError("The focus of the windows was lost, aborting", null);
                return;
            }

            FillInputSpan($"\"{nameof(ClipTypr)}-Transfer-{DateTime.UtcNow:yyyyMMddHHmmssff}Z.zip\"));", inputToSend, ref chunkSize);
            SendInputChunk(inputToSend, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
            if (!IsCorrectWindow(foregroundHWnd))
            {
                _logger.LogError("The focus of the windows was lost, aborting", null);
                return;
            }

            FillInputSpan("$b | % { $bytes=[Convert]::FromBase64String($_);$fs.Write($bytes,0,$bytes.Length) }; $fs.Close()", inputToSend, ref chunkSize);
            SendInputChunk(inputToSend, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
        }
    }

    private TimeSpan EstimateFileTransferRuntime()
    {
        try
        {
            var bytesLength = (ulong)new FileInfo(_tempZipPath).Length;

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
}