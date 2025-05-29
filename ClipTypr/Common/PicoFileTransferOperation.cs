namespace ClipTypr.Common;

public sealed class PicoFileTransferOperation : FileTransferOperationBase
{
    public override TimeSpan EstimatedRuntime { get; }

    public PicoFileTransferOperation(ILogger logger, ConfigurationHandler configHandler, string tempZipPath)
        : base(logger, configHandler, tempZipPath) => EstimatedRuntime = EstimateFileTransferRuntime();

    public override void Send() => throw new NotImplementedException();

    private TimeSpan EstimateFileTransferRuntime() => throw new NotImplementedException();
}