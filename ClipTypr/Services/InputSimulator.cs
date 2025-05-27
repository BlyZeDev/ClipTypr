namespace ClipTypr.Services;

using System.IO.Compression;

public sealed class InputSimulator : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConfigurationHandler _configHandler;

    public InputSimulator(ILogger logger, ConfigurationHandler configHandler)
    {
        _logger = logger;
        _configHandler = configHandler;
    }

    public TextTransferOperation CreateTextOperation(string text)
        => new TextTransferOperation(_logger, _configHandler, text);
    
    public FileTransferOperation CreateFileOperation(IEnumerable<string> files)
    {
        var tempZipPath = GetTempZipPath();

        using (var zipStream = new FileStream(tempZipPath, FileMode.Create))
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var path in files)
                {
                    if (File.Exists(path))
                    {
                        var entry = archive.CreateEntry(Path.GetFileName(path), CompressionLevel.SmallestSize);

                        using (var entryStream = entry.Open())
                        {
                            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                    }
                    else if (Directory.Exists(path))
                    {
                        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        {
                            var entry = archive.CreateEntry(Path.Combine(Path.GetFileName(path), Path.GetRelativePath(path, file).Replace(Path.DirectorySeparatorChar, '/')), CompressionLevel.SmallestSize);

                            using (var entryStream = entry.Open())
                            {
                                using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                                {
                                    fileStream.CopyTo(entryStream);
                                }
                            }
                        }
                    }
                    else _logger.LogWarning($"The file or directory does not exist: {path}");
                }
            }
        }

        _logger.LogInfo("Temporary .zip file created, do not touch");
        _logger.LogDebug(tempZipPath);

        return new FileTransferOperation(_logger, _configHandler, tempZipPath);
    }

    public void Dispose()
    {
        var path = GetTempZipPath();

        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInfo("Temporary .zip file cleaned up");
        }

        GC.SuppressFinalize(this);
    }

    private static string GetTempZipPath() => Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-TempFileTransfer.zip");
}