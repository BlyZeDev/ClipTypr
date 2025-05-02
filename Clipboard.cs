namespace StrokeMyKeys;

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
                            Span<char> buffer = stackalloc char[4096];
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
}