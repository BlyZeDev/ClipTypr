namespace ClipTypr.Common;

public abstract class FileTransferOperationBase : TransferOperationBase
{
    protected const int BufferSize = ChunkSize / 2;

    protected readonly string _tempZipPath;

    public abstract TimeSpan EstimatedRuntime { get; }
    
    protected FileTransferOperationBase(ILogger logger, ConfigurationHandler configHandler, string tempZipPath)
        : base(logger, configHandler) => _tempZipPath = tempZipPath;
}
