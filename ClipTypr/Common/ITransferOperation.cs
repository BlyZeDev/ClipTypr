namespace ClipTypr.Common;

public interface ITransferOperation
{
    public TimeSpan EstimatedRuntime { get; }
    public void Send();
}