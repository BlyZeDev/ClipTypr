namespace ClipTypr.Common;

public sealed class TextTransferOperation : TransferOperationBase
{
    private readonly string _text;

    public TextTransferOperation(ILogger logger, ConfigurationHandler configHandler, string text)
        : base(logger, configHandler) => _text = PrepareForSimulation(text);

    public override void Send()
    {
        _logger.LogInfo($"Starting to transfer {_text.Length} characters");

        var foregroundHWnd = Native.GetForegroundWindow();
        if (foregroundHWnd == nint.Zero)
        {
            _logger.LogError("Could not fetch the current foreground window, aborting", Native.TryGetError());
            return;
        }

        var textSpan = _text.AsSpan();
        Span<INPUT> input = stackalloc INPUT[ChunkSize * 2];

        var chunkSize = 0u;
        for (int i = 0; i < textSpan.Length; i += ChunkSize)
        {
            FillInputSpan(textSpan.Slice(i, Math.Min(ChunkSize, textSpan.Length - i)), input, ref chunkSize);
            SendInputChunk(input, chunkSize);

            Thread.Sleep(GetTimeout(chunkSize));
            if (!IsCorrectWindow(foregroundHWnd))
            {
                _logger.LogError("The focus of the windows was lost, aborting", null);
                return;
            }
        }
    }

    private static string PrepareForSimulation(string text) => text.Replace(Environment.NewLine, "\r", StringComparison.Ordinal).Replace('\n', '\r');
}