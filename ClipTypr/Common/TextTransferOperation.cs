namespace ClipTypr.Common;

public sealed class TextTransferOperation : TransferOperationBase
{
    private readonly string _text;
    private readonly int _iterations;
    private readonly int _remainder;

    public TextTransferOperation(ILogger logger, ConfigurationHandler configHandler, string text)
        : base(logger, configHandler)
    {
        _text = text;
        _iterations = _text.Length / ChunkSize;
        _remainder = _text.Length % ChunkSize;
    }

    public override void Send()
    {
        _logger.LogInfo($"Starting to transfer {_text.Length} characters");

        var foregroundHWnd = Native.GetForegroundWindow();
        if (foregroundHWnd == nint.Zero)
        {
            _logger.LogError("Could not fetch the current foreground window, aborting", Native.GetError());
            return;
        }

        var textSpan = _text.AsSpan();
        Span<INPUT> input = stackalloc INPUT[ChunkSize];

        var chunkSize = 0u;
        if (_iterations == 1)
        {
            FillInputSpan(textSpan, input, ref chunkSize);
            SendInputChunk(input, chunkSize);
            Thread.Sleep(GetTimeout(chunkSize));
            return;
        }

        for (int i = 0; i < _iterations; i++)
        {
            FillInputSpan(textSpan.Slice(i * ChunkSize, ChunkSize), input, ref chunkSize);
            SendInputChunk(input, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
            if (!IsCorrectWindow(foregroundHWnd))
            {
                _logger.LogError("The focus of the windows was lost, aborting", null);
                return;
            }
        }

        if (_remainder > 0)
        {
            FillInputSpan(textSpan.Slice(_iterations * ChunkSize, _remainder), input, ref chunkSize);
            SendInputChunk(input, chunkSize);
            Thread.Sleep(GetTimeout(chunkSize));
        }
    }
}