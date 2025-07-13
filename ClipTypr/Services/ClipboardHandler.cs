namespace ClipTypr.Services;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public sealed class ClipboardHandler
{
    private const int WindowsMaxPath = 260;
    private static readonly uint[] _clipboardPriorityFormats =
    [
        (uint)ClipboardFormat.UnicodeText,
        (uint)ClipboardFormat.DibV5,
        (uint)ClipboardFormat.Files
    ];

    private readonly ILogger _logger;

    public ClipboardHandler(ILogger logger) => _logger = logger;

    public ClipboardFormat GetCurrentFormat()
    {
        _logger.LogDebug("Trying to get the current clipboard format");

        try
        {
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.TryGetError());
                return ClipboardFormat.None;
            }

            var format = Native.GetPriorityClipboardFormat(_clipboardPriorityFormats, _clipboardPriorityFormats.Length);
            if (format < 0)
            {
                _logger.LogWarning("Could not fetch the correct format", Native.TryGetError());
                return ClipboardFormat.None;
            }

            if (!Enum.IsDefined((ClipboardFormat)format))
            {
                _logger.LogWarning("This format is not supported", Native.TryGetError());
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
            Native.CloseClipboard();
        }
    }

    public unsafe string? GetText()
    {
        _logger.LogDebug("Trying to get unicode text from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_UNICODETEXT))
            {
                _logger.LogWarning("Clipboard is not available", Native.TryGetError());
                return null;
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.TryGetError());
                return null;
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_UNICODETEXT);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", Native.TryGetError());
                return null;
            }

            try
            {
                var lockHandle = Native.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    _logger.LogWarning("Couldn't create a global lock", Native.TryGetError());
                    return null;
                }

                return Marshal.PtrToStringUni(lockHandle);
            }
            finally
            {
                Native.GlobalUnlock(clipboardHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            return null;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public unsafe Bitmap? GetBitmap()
    {
        _logger.LogDebug("Trying to get a bitmap from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_DIBV5))
            {
                _logger.LogWarning("Clipboard is not available", Native.TryGetError());
                return null;
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.TryGetError());
                return null;
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_DIBV5);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", Native.TryGetError());
                return null;
            }

            try
            {
                var lockHandle = Native.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    _logger.LogWarning("Couldn't create a global lock", Native.TryGetError());
                    return null;
                }

                var header = Marshal.PtrToStructure<BITMAPV5HEADER>(lockHandle);
                var offset = header.bV5ClrUsed * Marshal.SizeOf<RGBQUAD>() + header.bV5Size;

                if (header.bV5Compression == Native.BI_BITFIELDS) offset += 12;

                var bitmap = new Bitmap(header.bV5Width, header.bV5Height, (int)(header.bV5SizeImage / header.bV5Height), PixelFormat.Format32bppArgb, new nint((byte*)lockHandle + offset));
                if (header.bV5Height > 0) bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                return bitmap;
            }
            finally
            {
                Native.GlobalUnlock(clipboardHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            return null;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public unsafe IReadOnlyList<string> GetFiles()
    {
        _logger.LogDebug("Trying to get multiple files from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_HDROP))
            {
                _logger.LogWarning("Clipboard is not available", Native.TryGetError());
                return [];
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.TryGetError());
                return [];
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_HDROP);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", Native.TryGetError());
                return [];
            }

            var fileCount = Native.DragQueryFile(clipboardHandle, 0xFFFFFFFF, nint.Zero, 0);

            Span<char> buffer = stackalloc char[WindowsMaxPath + 1];

            var files = new List<string>((int)fileCount);
            for (uint i = 0; i < fileCount; i++)
            {
                var length = Native.DragQueryFile(clipboardHandle, i, nint.Zero, 0);
                if (length == 0)
                {
                    _logger.LogWarning($"Couldn't get the length of query file no.{i}", Native.TryGetError());
                    continue;
                }

                fixed (char* bufferPtr = buffer)
                {
                    var result = Native.DragQueryFile(clipboardHandle, i, (nint)bufferPtr, length + 1);

                    if (result == 0) _logger.LogWarning($"Couldn't get the query file no.{i}", Native.TryGetError());
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
            Native.CloseClipboard();
        }
    }

    public unsafe void SetText(in ReadOnlySpan<char> text)
    {
        _logger.LogDebug("Trying to add text to the clipboard");

        try
        {
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.TryGetError());
                return;
            }

            if (!Native.EmptyClipboard())
            {
                _logger.LogWarning("Could not empty the clipboard", Native.TryGetError());
                return;
            }

            var bytes = (text.Length + 1) * sizeof(char);

            var globalHandle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (nuint)bytes);
            if (globalHandle == nint.Zero)
            {
                _logger.LogWarning($"Couldn't globally allocate {bytes} bytes", Native.TryGetError());
                return;
            }

            try
            {
                try
                {
                    var lockHandle = Native.GlobalLock(globalHandle);
                    if (lockHandle == nint.Zero)
                    {
                        _logger.LogWarning("Couldn't create a global lock", Native.TryGetError());
                        return;
                    }

                    var destination = new Span<char>((void*)lockHandle, text.Length + 1);
                    text.CopyTo(destination);
                    destination[text.Length] = char.MinValue;
                }
                finally
                {
                    Native.GlobalUnlock(globalHandle);
                }

                if (Native.SetClipboardData(Native.CF_UNICODETEXT, globalHandle) == nint.Zero) _logger.LogWarning("Could not set the clipboard data", Native.TryGetError());
                else globalHandle = nint.Zero;
            }
            finally
            {
                if (globalHandle != nint.Zero) Native.GlobalFree(globalHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public unsafe void SetBitmap(Bitmap bitmap)
    {
        _logger.LogDebug("Trying to add bitmap to the clipboard");

        try
        {
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.TryGetError());
                return;
            }

            if (!Native.EmptyClipboard())
            {
                _logger.LogWarning("Could not empty the clipboard", Native.TryGetError());
                return;
            }

            using (var bitmapStream = new MemoryStream())
            {
                bitmap.Save(bitmapStream, ImageFormat.MemoryBmp);

                var length = bitmapStream.Length;

                var globalHandle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (nuint)length);
                if (globalHandle == nint.Zero)
                {
                    _logger.LogWarning($"Couldn't globally allocate {length} bytes", Native.TryGetError());
                    return;
                }

                try
                {
                    try
                    {
                        var lockHandle = Native.GlobalLock(globalHandle);
                        if (lockHandle == nint.Zero)
                        {
                            _logger.LogWarning("Couldn't create a global lock", Native.TryGetError());
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
                        Native.GlobalUnlock(globalHandle);
                    }

                    if (Native.SetClipboardData(Native.CF_DIBV5, globalHandle) == nint.Zero) _logger.LogWarning("Could not set the clipboard data", Native.TryGetError());
                    else globalHandle = nint.Zero;
                }
                finally
                {
                    if (globalHandle != nint.Zero) Native.GlobalFree(globalHandle);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public unsafe void SetFiles(IReadOnlyList<string> files)
    {
        _logger.LogDebug("Trying to add multiple files to the clipboard");

        if (files.Count == 0) return;

        try
        {
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.TryGetError());
                return;
            }

            if (!Native.EmptyClipboard())
            {
                _logger.LogWarning("Could not empty the clipboard", Native.TryGetError());
                return;
            }

            var dropFileSize = Marshal.SizeOf<DROPFILES>();
            var fileListBytesLength = sizeof(char) * (files.Sum(f => f.Length + 1) + 1);
            var totalSize = dropFileSize + fileListBytesLength;

            var globalHandle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (nuint)totalSize);
            if (globalHandle == nint.Zero)
            {
                _logger.LogWarning($"Couldn't globally allocate {totalSize} bytes", Native.TryGetError());
                return;
            }

            try
            {
                try
                {
                    var lockPtr = (byte*)Native.GlobalLock(globalHandle);
                    if (lockPtr is null)
                    {
                        _logger.LogWarning("Couldn't create a global lock", Native.TryGetError());
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
                    Native.GlobalUnlock(globalHandle);
                }

                if (Native.SetClipboardData(Native.CF_HDROP, globalHandle) == nint.Zero) _logger.LogWarning("Could not set the clipboard data", Native.TryGetError());
                else globalHandle = nint.Zero;
            }
            finally
            {
                if (globalHandle != nint.Zero) Native.GlobalFree(globalHandle);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
        finally
        {
            Native.CloseClipboard();
        }
    }
}