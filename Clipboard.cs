namespace StrokeMyKeys;

using System.Runtime.InteropServices;
using System.Text;

public static class Clipboard
{
    public static unsafe string? GetText()
    {
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

    public static unsafe IReadOnlyList<string> GetFiles()
    {
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
                if (result == 0)
                {
                    Logger.LogWarning($"Couldn't get the query file no.{i}", Native.GetError());
                }
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
}