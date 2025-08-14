namespace ClipTypr.Common;

public sealed record FilesClipboardEntry : ClipboardEntry
{
    public IReadOnlyList<string> Files { get; }

    public FilesClipboardEntry(IReadOnlyList<string> files) : base(GetDisplayText(files)) => Files = files;

    private static string GetDisplayText(IReadOnlyList<string> files)
    {
        return files.Count switch
        {
            0 => "No files",
            1 => $"🗂️ - {Path.GetFileName(files[0])}",
            _ => $"🗂️ - {Path.GetFileName(files[0])} + {files.Count - 1} more",
        };
    }
}