namespace ClipTypr.Common;

public abstract class TransferOperationBase
{
    protected const int ChunkSize = Util.StackSizeBytes / 128;
    protected const int BufferSize = ChunkSize / 2;

    protected readonly ILogger _logger;

    protected TransferOperationBase(ILogger logger) => _logger = logger;

    protected static bool IsCorrectWindow(nint originalHWnd) => originalHWnd == Native.GetForegroundWindow();
}