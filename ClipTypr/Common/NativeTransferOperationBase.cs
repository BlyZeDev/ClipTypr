namespace ClipTypr.Common;

public abstract class NativeTransferOperationBase : TransferOperationBase
{
    private const ushort Enter = (ushort)ConsoleKey.Enter;
    private const ushort Tab = (ushort)ConsoleKey.Tab;

    protected static readonly Dictionary<TransferSecurity, double> _transferTimeoutMultipliers = new Dictionary<TransferSecurity, double>
    {
        { TransferSecurity.VeryUnsafe, 0.25 },
        { TransferSecurity.Unsafe, 0.5 },
        { TransferSecurity.Average, 1 },
        { TransferSecurity.Safe, 2 },
        { TransferSecurity.VerySafe, 4 },
        { TransferSecurity.Guaranteed, 8 }
    };

    protected readonly ConfigurationHandler _configHandler;

    protected NativeTransferOperationBase(ILogger logger, ConfigurationHandler configHandler) : base(logger) => _configHandler = configHandler;

    protected int GetTimeout(uint chunkSize) => (int)(chunkSize * _transferTimeoutMultipliers[_configHandler.Current.TransferSecurity]);

    protected static void FillInput(in ReadOnlySpan<char> characters, in Span<INPUT> input, ref uint chunkSize)
    {
        ushort vk;
        ushort scanCode;
        uint flagsDown;
        uint flagsUp;

        for (int i = 0; i < characters.Length; i++)
        {
            ref readonly var character = ref characters[i];

            vk = 0;
            scanCode = character;
            flagsDown = Native.KEYEVENTF_UNICODE;
            flagsUp = Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP;

            switch (character)
            {
                case '\r':
                    vk = Enter;
                    scanCode = 0;
                    flagsDown = Native.KEYEVENTF_EXTENDEDKEY;
                    flagsUp = Native.KEYEVENTF_KEYUP | Native.KEYEVENTF_EXTENDEDKEY;
                    break;

                case '\t':
                    vk = Tab;
                    scanCode = 0;
                    flagsDown = 0;
                    flagsUp = Native.KEYEVENTF_KEYUP;
                    break;

                default:
                    if ((character & 0xFF00) == 0xE000)
                    {
                        flagsDown |= Native.KEYEVENTF_EXTENDEDKEY;
                        flagsUp |= Native.KEYEVENTF_EXTENDEDKEY;
                    }
                    break;
            }

            input[i * 2] = new INPUT
            {
                Type = Native.INPUT_KEYBOARD,
                Union = new INPUT_UNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        KeyCode = vk,
                        Scan = scanCode,
                        Flags = flagsDown,
                        Time = 0,
                        ExtraInfo = Native.GetMessageExtraInfo()
                    }
                }
            };

            input[i * 2 + 1] = new INPUT
            {
                Type = Native.INPUT_KEYBOARD,
                Union = new INPUT_UNION
                {
                    Keyboard = new KEYBDINPUT
                    {
                        KeyCode = vk,
                        Scan = scanCode,
                        Flags = flagsUp,
                        Time = 0,
                        ExtraInfo = Native.GetMessageExtraInfo()
                    }
                }
            };
        }

        chunkSize = (uint)(characters.Length * 2);
    }

    protected unsafe void SendInputChunk(in ReadOnlySpan<INPUT> input, uint length)
    {
        fixed (INPUT* inputPtr = input)
        {
            var inputSent = Native.SendInput(length, inputPtr, sizeof(INPUT));

            if (inputSent == 0) _logger.LogError("Couldn't send inputs", Native.TryGetError());
            else if (inputSent != length) _logger.LogWarning($"{Math.Abs(length - inputSent)} inputs were lost", Native.TryGetError());
            else _logger.LogDebug($"Successfully sent {inputSent} inputs");
        }
    }
}