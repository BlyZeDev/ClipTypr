namespace ClipTypr.Services;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public sealed class ClipboardHandler : IDisposable
{
    private const int WindowsMaxPath = 260;
    private const int DebounceDelayMs = 1000;
    private const int RetryCount = 50;
    private const int RetryDelayMs = 20;

    private static readonly uint[] _clipboardPriorityFormats =
    [
        PInvoke.CF_UNICODETEXT,
        PInvoke.CF_DIBV5,
        PInvoke.CF_HDROP
    ];

    private readonly ILogger _logger;
    private readonly NativeMessageHandler _messageHandler;

    private long lastUpdateTicks;

    public event Action? ClipboardUpdate;

    public ClipboardHandler(ILogger logger, NativeMessageHandler messageHandler)
    {
        _logger = logger;
        _messageHandler = messageHandler;
        _messageHandler.WndProc += WndProcFunc;

        PInvoke.AddClipboardFormatListener(_messageHandler.HWnd);
    }

    public ClipboardFormat GetCurrentFormat()
    {
        _logger.LogDebug("Trying to get the current clipboard format");

        try
        {
            if (!TryOpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", PInvoke.TryGetError());
                return ClipboardFormat.None;
            }

            var format = PInvoke.GetPriorityClipboardFormat(_clipboardPriorityFormats, _clipboardPriorityFormats.Length);
            if (format < 0)
            {
                _logger.LogWarning("Could not fetch the correct format", PInvoke.TryGetError());
                return ClipboardFormat.None;
            }

            if (!Enum.IsDefined((ClipboardFormat)format))
            {
                _logger.LogWarning("This format is not supported", PInvoke.TryGetError());
                return ClipboardFormat.None;
            }

            return (ClipboardFormat)format;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            return ClipboardFormat.None;
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public unsafe string? GetText()
    {
        _logger.LogDebug("Trying to get unicode text from the clipboard");

        try
        {
            if (!PInvoke.IsClipboardFormatAvailable(PInvoke.CF_UNICODETEXT))
            {
                _logger.LogWarning("Clipboard is not available", PInvoke.TryGetError());
                return null;
            }
            if (!TryOpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", PInvoke.TryGetError());
                return null;
            }

            var clipboardHandle = PInvoke.GetClipboardData(PInvoke.CF_UNICODETEXT);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", PInvoke.TryGetError());
                return null;
            }

            try
            {
                var lockHandle = PInvoke.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    _logger.LogWarning("Couldn't create a global lock", PInvoke.TryGetError());
                    return null;
                }

                return Marshal.PtrToStringUni(lockHandle);
            }
            finally
            {
                PInvoke.GlobalUnlock(clipboardHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            return null;
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public unsafe Bitmap? GetBitmap()
    {
        _logger.LogDebug("Trying to get a bitmap from the clipboard");

        try
        {
            if (!PInvoke.IsClipboardFormatAvailable(PInvoke.CF_DIBV5))
            {
                _logger.LogWarning("Clipboard is not available", PInvoke.TryGetError());
                return null;
            }
            if (!TryOpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", PInvoke.TryGetError());
                return null;
            }

            var clipboardHandle = PInvoke.GetClipboardData(PInvoke.CF_DIBV5);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", PInvoke.TryGetError());
                return null;
            }

            try
            {
                var lockHandle = PInvoke.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    _logger.LogWarning("Couldn't create a global lock", PInvoke.TryGetError());
                    return null;
                }

                var header = Marshal.PtrToStructure<BITMAPV5HEADER>(lockHandle);

                var paletteSize = header.bV5ClrUsed * Marshal.SizeOf<RGBQUAD>();
                if (header.bV5Compression == PInvoke.BI_BITFIELDS) paletteSize += 12;

                var pixelDataPtr = nint.Add(lockHandle, (int)header.bV5Size + paletteSize);

                var stride = (int)(header.bV5SizeImage / Math.Abs(header.bV5Height));
                if (stride <= 0) stride = (header.bV5Width * 32 + 31) / 32 * 4;

                var bitmap = new Bitmap(header.bV5Width, Math.Abs(header.bV5Height), stride, PixelFormat.Format32bppArgb, pixelDataPtr);
                if (header.bV5Height > 0) bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                return bitmap;
            }
            finally
            {
                PInvoke.GlobalUnlock(clipboardHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            return null;
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public unsafe IReadOnlyList<string> GetFiles()
    {
        _logger.LogDebug("Trying to get multiple files from the clipboard");

        try
        {
            if (!PInvoke.IsClipboardFormatAvailable(PInvoke.CF_HDROP))
            {
                _logger.LogWarning("Clipboard is not available", PInvoke.TryGetError());
                return [];
            }
            if (!TryOpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", PInvoke.TryGetError());
                return [];
            }

            var clipboardHandle = PInvoke.GetClipboardData(PInvoke.CF_HDROP);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", PInvoke.TryGetError());
                return [];
            }

            var fileCount = PInvoke.DragQueryFile(clipboardHandle, 0xFFFFFFFF, nint.Zero, 0);

            Span<char> buffer = stackalloc char[WindowsMaxPath + 1];

            var files = new List<string>((int)fileCount);
            for (uint i = 0; i < fileCount; i++)
            {
                var length = PInvoke.DragQueryFile(clipboardHandle, i, nint.Zero, 0);
                if (length == 0)
                {
                    _logger.LogWarning($"Couldn't get the length of query file no.{i}", PInvoke.TryGetError());
                    continue;
                }

                fixed (char* bufferPtr = buffer)
                {
                    var result = PInvoke.DragQueryFile(clipboardHandle, i, (nint)bufferPtr, length + 1);

                    if (result == 0) _logger.LogWarning($"Couldn't get the query file no.{i}", PInvoke.TryGetError());
                    else files.Add(new string(bufferPtr, 0, (int)result));
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            return [];
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public unsafe void SetText(in ReadOnlySpan<char> text)
    {
        _logger.LogDebug("Trying to add text to the clipboard");

        try
        {
            if (!TryOpenClipboard(_messageHandler.HWnd))
            {
                _logger.LogWarning("Clipboard cannot be opened", PInvoke.TryGetError());
                return;
            }

            if (!PInvoke.EmptyClipboard())
            {
                _logger.LogWarning("Could not empty the clipboard", PInvoke.TryGetError());
                return;
            }

            var bytes = (text.Length + 1) * sizeof(char);

            var globalHandle = PInvoke.GlobalAlloc(PInvoke.GMEM_MOVEABLE, (nuint)bytes);
            if (globalHandle == nint.Zero)
            {
                _logger.LogWarning($"Couldn't globally allocate {bytes} bytes", PInvoke.TryGetError());
                return;
            }

            try
            {
                try
                {
                    var lockHandle = PInvoke.GlobalLock(globalHandle);
                    if (lockHandle == nint.Zero)
                    {
                        _logger.LogWarning("Couldn't create a global lock", PInvoke.TryGetError());
                        return;
                    }

                    var destination = new Span<char>((void*)lockHandle, text.Length + 1);
                    text.CopyTo(destination);
                    destination[text.Length] = char.MinValue;
                }
                finally
                {
                    PInvoke.GlobalUnlock(globalHandle);
                }

                if (PInvoke.SetClipboardData(PInvoke.CF_UNICODETEXT, globalHandle) == nint.Zero) _logger.LogWarning("Could not set the clipboard data", PInvoke.TryGetError());
                else globalHandle = nint.Zero;
            }
            finally
            {
                if (globalHandle != nint.Zero) PInvoke.GlobalFree(globalHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public unsafe void SetBitmap(Bitmap bitmap)
    {
        _logger.LogDebug("Trying to add bitmap to the clipboard");

        try
        {
            if (!TryOpenClipboard(_messageHandler.HWnd))
            {
                _logger.LogWarning("Clipboard cannot be opened", PInvoke.TryGetError());
                return;
            }

            if (!PInvoke.EmptyClipboard())
            {
                _logger.LogWarning("Could not empty the clipboard", PInvoke.TryGetError());
                return;
            }

            using (var bitmapStream = new MemoryStream())
            {
                bitmap.Save(bitmapStream, ImageFormat.MemoryBmp);

                var length = bitmapStream.Length;

                var globalHandle = PInvoke.GlobalAlloc(PInvoke.GMEM_MOVEABLE, (nuint)length);
                if (globalHandle == nint.Zero)
                {
                    _logger.LogWarning($"Couldn't globally allocate {length} bytes", PInvoke.TryGetError());
                    return;
                }

                try
                {
                    try
                    {
                        var lockHandle = PInvoke.GlobalLock(globalHandle);
                        if (lockHandle == nint.Zero)
                        {
                            _logger.LogWarning("Couldn't create a global lock", PInvoke.TryGetError());
                            return;
                        }

                        bitmapStream.Position = 0;
                        using (var targetStream = new UnmanagedMemoryStream((byte*)lockHandle, length, length, FileAccess.Write))
                        {
                            bitmapStream.CopyTo(targetStream);
                        }
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(globalHandle);
                    }

                    if (PInvoke.SetClipboardData(PInvoke.CF_DIBV5, globalHandle) == nint.Zero) _logger.LogWarning("Could not set the clipboard data", PInvoke.TryGetError());
                    else globalHandle = nint.Zero;
                }
                finally
                {
                    if (globalHandle != nint.Zero) PInvoke.GlobalFree(globalHandle);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public unsafe void SetFiles(IReadOnlyList<string> files)
    {
        _logger.LogDebug("Trying to add multiple files to the clipboard");

        if (files.Count == 0) return;

        try
        {
            if (!TryOpenClipboard(_messageHandler.HWnd))
            {
                _logger.LogWarning("Clipboard cannot be opened", PInvoke.TryGetError());
                return;
            }

            if (!PInvoke.EmptyClipboard())
            {
                _logger.LogWarning("Could not empty the clipboard", PInvoke.TryGetError());
                return;
            }

            var dropFileSize = Marshal.SizeOf<DROPFILES>();
            var fileListBytesLength = sizeof(char) * (files.Sum(f => f.Length + 1) + 1);
            var totalSize = dropFileSize + fileListBytesLength;

            var globalHandle = PInvoke.GlobalAlloc(PInvoke.GMEM_MOVEABLE, (nuint)totalSize);
            if (globalHandle == nint.Zero)
            {
                _logger.LogWarning($"Couldn't globally allocate {totalSize} bytes", PInvoke.TryGetError());
                return;
            }

            try
            {
                try
                {
                    var lockPtr = (byte*)PInvoke.GlobalLock(globalHandle);
                    if (lockPtr is null)
                    {
                        _logger.LogWarning("Couldn't create a global lock", PInvoke.TryGetError());
                        return;
                    }

                    var dropFiles = new DROPFILES
                    {
                        pFiles = (uint)dropFileSize,
                        fWide = true
                    };
                    *(DROPFILES*)lockPtr = dropFiles;

                    var stringPtr = (char*)(lockPtr + dropFileSize);
                    foreach (var file in files)
                    {
                        fixed (char* filePtr = file)
                        {
                            Buffer.MemoryCopy(filePtr, stringPtr, file.Length * sizeof(char), file.Length * sizeof(char));
                            stringPtr += file.Length;
                            *stringPtr++ = char.MinValue;
                        }
                    }
                    *stringPtr = char.MinValue;
                }
                finally
                {
                    PInvoke.GlobalUnlock(globalHandle);
                }

                if (PInvoke.SetClipboardData(PInvoke.CF_HDROP, globalHandle) == nint.Zero) _logger.LogWarning("Could not set the clipboard data", PInvoke.TryGetError());
                else globalHandle = nint.Zero;
            }
            finally
            {
                if (globalHandle != nint.Zero) PInvoke.GlobalFree(globalHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    public void Dispose()
    {
        PInvoke.RemoveClipboardFormatListener(_messageHandler.HWnd);

        _messageHandler.WndProc -= WndProcFunc;

        GC.SuppressFinalize(this);
    }

    private void WndProcFunc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg != PInvoke.WM_CLIPBOARDUPDATE) return;

        var nowTicks = Environment.TickCount64;
        if (nowTicks - lastUpdateTicks < DebounceDelayMs) return;

        lastUpdateTicks = nowTicks;
        ClipboardUpdate?.Invoke();
    }

    private static bool TryOpenClipboard(nint hWndNewOwner)
    {
        for (int i = 0; i < RetryCount; i++)
        {
            if (PInvoke.OpenClipboard(hWndNewOwner)) return true;

            Thread.Sleep(RetryDelayMs);
        }

        return false;
    }
}