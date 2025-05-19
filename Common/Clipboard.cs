namespace ClipTypr.Common;

using ClipTypr.NATIVE;
using System.Text;

public static class Clipboard
{
    public static unsafe string? GetText()
    {
        Logger.LogDebug("Trying to get unicode text from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_UNICODETEXT))
            {
                Logger.LogWarning("Clipboard is not available", Native.GetError());
                return null;
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                Logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return null;
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_UNICODETEXT);
            if (clipboardHandle == nint.Zero)
            {
                Logger.LogWarning("Couldn't get clipboard data", Native.GetError());
                return null;
            }

            try
            {
                var lockHandle = Native.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    Logger.LogWarning("Couldn't create a global lock", Native.GetError());
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
            Logger.LogError(ex.Message, ex);
            return null;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public static string? GetFile()
    {
        Logger.LogDebug("Trying to get a file from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_HDROP))
            {
                Logger.LogWarning("Clipboard is not available", Native.GetError());
                return null;
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                Logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return null;
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_HDROP);
            if (clipboardHandle == nint.Zero)
            {
                Logger.LogWarning("Couldn't get clipboard data", Native.GetError());
                return null;
            }

            var fileCount = Native.DragQueryFile(clipboardHandle, 0xFFFFFFFF, nint.Zero, 0);
            if (fileCount < 1) return null;

            var pathBuilder = new StringBuilder(1024);
            var result = Native.DragQueryFile(clipboardHandle, 0, pathBuilder, pathBuilder.Capacity);

            if (result == 0)
            {
                Logger.LogWarning($"Couldn't get the query file no.0", Native.GetError());
                return null;
            }

            return pathBuilder.ToString();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message, ex);
            return null;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public static IReadOnlyList<string> GetFiles()
    {
        Logger.LogDebug("Trying to get multiple files from the clipboard");

        try
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_HDROP))
            {
                Logger.LogWarning("Clipboard is not available", Native.GetError());
                return [];
            }
            if (!Native.OpenClipboard(nint.Zero))
            {
                Logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return [];
            }

            var clipboardHandle = Native.GetClipboardData(Native.CF_HDROP);
            if (clipboardHandle == nint.Zero)
            {
                Logger.LogWarning("Couldn't get clipboard data", Native.GetError());
                return [];
            }

            var fileCount = Native.DragQueryFile(clipboardHandle, 0xFFFFFFFF, nint.Zero, 0);

            var files = new List<string>((int)fileCount);
            for (uint i = 0; i < fileCount; i++)
            {
                var pathBuilder = new StringBuilder(1024);
                var result = Native.DragQueryFile(clipboardHandle, i, pathBuilder, pathBuilder.Capacity);

                if (result == 0) Logger.LogWarning($"Couldn't get the query file no.{i}", Native.GetError());
                else files.Add(pathBuilder.ToString());
            }

            return files;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message, ex);
            return [];
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public static unsafe void SetText(in ReadOnlySpan<char> text)
    {
        Logger.LogDebug("Trying to add text to the clipboard");

        try
        {
            if (!Native.OpenClipboard(nint.Zero))
            {
                Logger.LogWarning("Clipboard cannot be opened", Native.GetError());
                return;
            }

            if (!Native.EmptyClipboard())
            {
                Logger.LogWarning("Could not empty the clipboard", Native.GetError());
                return;
            }

            var bytes = (text.Length + 1) * sizeof(char);
            var clipboardHandle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (nuint)bytes);
            if (clipboardHandle == nint.Zero)
            {
                Logger.LogWarning($"Couldn't globally allocate {bytes} bytes", Native.GetError());
                return;
            }

            try
            {
                var lockHandle = Native.GlobalLock(clipboardHandle);
                if (lockHandle == nint.Zero)
                {
                    Logger.LogWarning("Couldn't create a global lock", Native.GetError());
                    return;
                }

                Span<char> destination = new Span<char>((void*)lockHandle, text.Length + 1);
                text.CopyTo(destination);
                destination[text.Length] = char.MinValue;
            }
            finally
            {
                Native.GlobalUnlock(clipboardHandle);
            }

            if (Native.SetClipboardData(Native.CF_UNICODETEXT, clipboardHandle) == nint.Zero)
                Logger.LogWarning("Could not set the clipboard data", Native.GetError());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message, ex);
            return;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }
}