namespace ClipTypr.Services;

using System.Text;

public sealed class ClipboardService
{
    private const int WindowsMaxPath = 260;

    private readonly ILogger _logger;

    public ClipboardService(ILogger logger) => _logger = logger;

    public unsafe string? GetText()
    {
        _logger.LogDebug("Trying to get unicode text from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_UNICODETEXT))
            {
                _logger.LogWarning("Clipboard is not available", Native.GetError());
                return null;
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return null;
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_UNICODETEXT);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", Native.GetError());
                return null;
            }

            try
            {
                var lockHandle = Native.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    _logger.LogWarning("Couldn't create a global lock", Native.GetError());
                    return null;
                }

                using (var writer = new StringWriter())
                {
                    using (var stream = new UnmanagedMemoryStream((byte*)lockHandle, (long)Native.GlobalSize(lockHandle)))
                    {
                        using (var reader = new StreamReader(stream, Encoding.Unicode))
                        {
                            Span<char> buffer = stackalloc char[Util.StackSizeBytes];

                            int charCount;
                            while (!reader.EndOfStream && (charCount = reader.ReadBlock(buffer)) > 0)
                            {
                                if (reader.EndOfStream && buffer[charCount - 1] == char.MinValue) writer.Write(buffer[..(charCount - 1)]);
                                else writer.Write(buffer);
                            }
                        }
                    }

                    writer.Flush();
                    return writer.ToString();
                }
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

    public unsafe string? GetFile()
    {
        _logger.LogDebug("Trying to get a file from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_HDROP))
            {
                _logger.LogWarning("Clipboard is not available", Native.GetError());
                return null;
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return null;
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_HDROP);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", Native.GetError());
                return null;
            }

            var fileCount = Native.DragQueryFile(clipboardHandle, 0xFFFFFFFF, nint.Zero, 0);
            if (fileCount < 1) return null;

            var length = Native.DragQueryFile(clipboardHandle, 0, nint.Zero, 0);
            if (length == 0)
            {
                _logger.LogWarning("Couldn't get the length of query file no.0", Native.GetError());
                return null;
            }

            Span<char> buffer = stackalloc char[WindowsMaxPath + 1];

            fixed (char* bufferPtr = buffer)
            {
                var result = Native.DragQueryFile(clipboardHandle, 0, (nint)bufferPtr, length + 1);

                if (result == 0)
                {
                    _logger.LogWarning($"Couldn't get the query file no.0", Native.GetError());
                    return null;
                }

                return new string(bufferPtr, 0, (int)result);
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
                _logger.LogWarning("Clipboard is not available", Native.GetError());
                return [];
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                _logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return [];
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_HDROP);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning("Couldn't get clipboard data", Native.GetError());
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
                    _logger.LogWarning($"Couldn't get the length of query file no.{i}", Native.GetError());
                    continue;
                }

                fixed (char* bufferPtr = buffer)
                {
                    var result = Native.DragQueryFile(clipboardHandle, i, (nint)bufferPtr, length + 1);

                    if (result == 0) _logger.LogWarning($"Couldn't get the query file no.{i}", Native.GetError());
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
                _logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return;
            }

            if (!Native.EmptyClipboard())
            {
                _logger.LogWarning("Could not empty the clipboard", Native.GetError());
                return;
            }

            var bytes = (text.Length + 1) * sizeof(char);
            var clipboardHandle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (nuint)bytes);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning($"Couldn't globally allocate {bytes} bytes", Native.GetError());
                return;
            }

            try
            {
                var lockHandle = Native.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    _logger.LogWarning("Couldn't create a global lock", Native.GetError());
                    return;
                }

                var destination = new Span<char>((void*)lockHandle, text.Length + 1);
                text.CopyTo(destination);
                destination[text.Length] = char.MinValue;
            }
            finally
            {
                Native.GlobalUnlock(clipboardHandle);
            }

            if (Native.SetClipboardData(Native.CF_UNICODETEXT, clipboardHandle) == nint.Zero)
                _logger.LogWarning("Could not set the clipboard data", Native.GetError());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            return;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }
}