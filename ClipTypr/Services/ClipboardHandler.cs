﻿namespace ClipTypr.Services;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public sealed class ClipboardHandler
{
    private const int WindowsMaxPath = 260;
    private static readonly uint[] _clipboardPriorityFormats =
    [
        ClipboardFormat.UnicodeText,
        ClipboardFormat.DibV5,
        ClipboardFormat.Files,
        ClipboardFormat.Text,
        ClipboardFormat.Dib,
        ClipboardFormat.OemText,
        ClipboardFormat.Bitmap
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

            return new ClipboardFormat((uint)format);
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

    public Bitmap? GetBitmap()
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

                var header = Marshal.PtrToStructure<BITMAPV5HEADER>(clipboardHandle);
                var offset = header.bV5ClrUsed * Marshal.SizeOf<RGBQUAD>() + header.bV5Size;

                if (header.bV5Compression == Native.BI_BITFIELDS) offset += 12;

                var bitmap = new Bitmap(header.bV5Width, header.bV5Height, (int)(header.bV5SizeImage / header.bV5Height), PixelFormat.Format32bppArgb, new nint(clipboardHandle.ToInt64() + offset));
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
            var clipboardHandle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (nuint)bytes);
            if (clipboardHandle == nint.Zero)
            {
                _logger.LogWarning($"Couldn't globally allocate {bytes} bytes", Native.TryGetError());
                return;
            }

            try
            {
                var lockHandle = Native.GlobalLock(clipboardHandle);
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
                Native.GlobalUnlock(clipboardHandle);
            }

            if (Native.SetClipboardData(Native.CF_UNICODETEXT, clipboardHandle) == nint.Zero)
                _logger.LogWarning("Could not set the clipboard data", Native.TryGetError());
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