namespace ClipTypr.Transfer;

using System.Buffers.Text;
using System.IO.Ports;
using System.Text;

public sealed class PicoFileTransferOperation : TransferOperationBase, ITransferOperation
{
    private const byte Literal = (byte)'\'';
    private const byte Comma = (byte)',';

    private const int PicoBufferSize = 4096;
    private const int BaudRate = 115200;

    private readonly string _tempZipPath;
    private readonly string _comPort;

    public TimeSpan EstimatedRuntime { get; }

    public PicoFileTransferOperation(ILogger logger, string tempZipPath, string comPort) : base(logger)
    {
        _tempZipPath = tempZipPath;
        _comPort = comPort;
        EstimatedRuntime = EstimateFileTransferRuntime();
    }

    public void Send()
    {
        _logger.LogInfo("Starting to transfer the temporary .zip file");
        _logger.LogDebug($"Com Port: {_comPort}");

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

        var utf8 = new UTF8Encoding(false);
        using (var serialPort = new SerialPort(_comPort, BaudRate))
        {
            serialPort.DtrEnable = true;
            serialPort.RtsEnable = true;
            serialPort.Encoding = utf8;

            if (!serialPort.IsOpen)
            {
                try
                {
                    serialPort.Open();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not open Serial Port", ex);
                }
            }

            Thread.Sleep(500);

            serialPort.DiscardOutBuffer();

            var portStream = serialPort.BaseStream;

            int bytesRead;
            Span<byte> buffer = stackalloc byte[Base64.GetMaxEncodedToUtf8Length(BufferSize)];

            using (var fileStream = new FileStream(_tempZipPath, FileMode.Open, FileAccess.Read, FileShare.None, BufferSize))
            {
                portStream.Write(utf8.GetBytes("$b=("));

                if (!IsCorrectWindow(foregroundHWnd))
                {
                    _logger.LogError("The focus of the windows was lost, aborting", null);
                    serialPort.DiscardOutBuffer();
                    return;
                }

                while ((bytesRead = fileStream.Read(buffer[..BufferSize])) > 0)
                {
                    Base64.EncodeToUtf8InPlace(buffer, bytesRead, out var bytesWritten);

                    portStream.WriteByte(Literal);
                    portStream.Write(buffer[..bytesWritten]);
                    portStream.WriteByte(Literal);

                    if (fileStream.Position < fileStream.Length) portStream.WriteByte(Comma);

                    if (!IsCorrectWindow(foregroundHWnd))
                    {
                        _logger.LogError("The focus of the windows was lost, aborting", null);
                        serialPort.DiscardOutBuffer();
                        return;
                    }
                }

                portStream.Write(utf8.GetBytes($");$fs=[System.IO.File]::OpenWrite((Join-Path (Get-Location).Path "));

                if (!IsCorrectWindow(foregroundHWnd))
                {
                    _logger.LogError("The focus of the windows was lost, aborting", null);
                    serialPort.DiscardOutBuffer();
                    return;
                }

                portStream.Write(utf8.GetBytes($"\'{nameof(ClipTypr)}-Transfer-{DateTime.UtcNow:yyyyMMddHHmmssff}Z.zip\'));"));

                if (!IsCorrectWindow(foregroundHWnd))
                {
                    _logger.LogError("The focus of the windows was lost, aborting", null);
                    serialPort.DiscardOutBuffer();
                    return;
                }

                portStream.Write(utf8.GetBytes("$b | % { $bytes=[Convert]::FromBase64String($_);$fs.Write($bytes,0,$bytes.Length) }; $fs.Close()"));
            }
        }
    }

    private TimeSpan EstimateFileTransferRuntime()
    {
        const double ApproxCharsPerSecond = 15;

        try
        {
            var bytesLength = (ulong)new FileInfo(_tempZipPath).Length;
            _logger.LogDebug($".zip file size: {bytesLength} Bytes");

            var encodedLength = 4 * ((bytesLength + 2) / 3);

            return TimeSpan.FromSeconds(encodedLength > ApproxCharsPerSecond ? encodedLength / ApproxCharsPerSecond : 1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not estimate transfer time", ex);
            return TimeSpan.Zero;
        }
    }
}