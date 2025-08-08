namespace ClipTypr.Transfer;

public sealed class TextTransferOperation : NativeTransferOperationBase, ITransferOperation
{
    private readonly string _text;

    public TimeSpan EstimatedRuntime { get; }

    public TextTransferOperation(ILogger logger, ConfigurationHandler configHandler, string text)
        : base(logger, configHandler)
    {
        _text = PrepareForSimulation(text);
        EstimatedRuntime = TimeSpan.FromMilliseconds(_text.Length);
    }

    public void Send()
    {
        _logger.LogInfo($"Starting to transfer {_text.Length} characters");

        var foregroundHWnd = PInvoke.GetForegroundWindow();
        if (foregroundHWnd == nint.Zero)
        {
            _logger.LogError("Could not fetch the current foreground window, aborting", PInvoke.TryGetError());
            return;
        }

        var textSpan = _text.AsSpan();
        Span<INPUT> input = stackalloc INPUT[ChunkSize * 2];

        var chunkSize = 0u;
        for (var i = 0; i < textSpan.Length; i += ChunkSize)
        {
            FillInput(textSpan.Slice(i, Math.Min(ChunkSize, textSpan.Length - i)), input, ref chunkSize);
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