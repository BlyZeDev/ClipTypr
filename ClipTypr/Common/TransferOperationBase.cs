namespace ClipTypr.Common;

public abstract class TransferOperationBase
{
    protected const int ChunkSize = Util.StackSizeBytes / 128;
    protected static readonly Dictionary<TransferSecurity, double> _transferTimeoutMultipliers = new Dictionary<TransferSecurity, double>
    {
        { TransferSecurity.VeryUnsafe, 0.25 },
        { TransferSecurity.Unsafe, 0.5 },
        { TransferSecurity.Average, 1 },
        { TransferSecurity.Safe, 2 },
        { TransferSecurity.VerySafe, 4 },
        { TransferSecurity.Guaranteed, 8 }
    };
    
    protected readonly ILogger _logger;
    protected readonly ConfigurationHandler _configHandler;

    protected TransferOperationBase(ILogger logger, ConfigurationHandler configHandler)
    {
        _logger = logger;
        _configHandler = configHandler;
    }

    public abstract void Send();

    protected unsafe void SendInputChunk(in ReadOnlySpan<INPUT> input, uint length)
    {
        fixed (INPUT* inputPtr = input)
        {
            var inputSent = Native.SendInput(length, inputPtr, sizeof(INPUT));

            if (inputSent == 0) _logger.LogError("Couldn't send inputs", Native.GetError());
            else if (inputSent != length) _logger.LogWarning($"{Math.Abs(length - inputSent)} inputs were lost", Native.GetError());
            else _logger.LogDebug($"Successfully sent {inputSent} inputs");
        }
    }

    protected int GetTimeout(uint chunkSize) => (int)(chunkSize * _transferTimeoutMultipliers[_configHandler.Current.TransferSecurity]);

    protected static void FillInputSpan(in ReadOnlySpan<char> characters, in Span<INPUT> input, ref uint chunkSize)
    {
        for (var i = 0; i < characters.Length; i++)
        {
            ref readonly var scanCode = ref characters[i];

            ref var down = ref input[i * 2];
            ref var up = ref input[i * 2 + 1];

            down = new INPUT
            {
                Type = Native.INPUT_KEYBOARD,
                Union = new INPUT_UNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        KeyCode = 0,
                        Scan = scanCode,
                        Flags = Native.KEYEVENTF_UNICODE,
                        Time = 0,
                        ExtraInfo = Native.GetMessageExtraInfo()
                    }
                }
            };

            up = new INPUT
            {
                Type = Native.INPUT_KEYBOARD,
                Union = new INPUT_UNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        KeyCode = 0,
                        Scan = scanCode,
                        Flags = Native.KEYEVENTF_KEYUP | Native.KEYEVENTF_UNICODE,
                        Time = 0,
                        ExtraInfo = Native.GetMessageExtraInfo()
                    }
                }
            };

            if ((scanCode & 0xFF00) == 0xE000)
            {
                down.Union.Keyboard.Flags |= Native.KEYEVENTF_EXTENDEDKEY;
                up.Union.Keyboard.Flags |= Native.KEYEVENTF_EXTENDEDKEY;
            }
        }

        chunkSize = (uint)(characters.Length * 2);
    }

    protected static bool IsCorrectWindow(nint originalHWnd) => originalHWnd == Native.GetForegroundWindow();
}