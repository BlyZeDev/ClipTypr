namespace ClipTypr.Transfer;

public interface ITransferOperation
{
    public TimeSpan EstimatedRuntime { get; }
    public void Send();
}