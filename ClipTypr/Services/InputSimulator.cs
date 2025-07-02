namespace ClipTypr.Services;

using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

public sealed partial class InputSimulator : IDisposable
{
    private const string PicoVID = "2E8A";
    private const string PicoPID = "00C0";

    private readonly ILogger _logger;
    private readonly ConfigurationHandler _configHandler;

    public InputSimulator(ILogger logger, ConfigurationHandler configHandler)
    {
        _logger = logger;
        _configHandler = configHandler;
    }

    public TextTransferOperation CreateTextOperation(string text) => new TextTransferOperation(_logger, _configHandler, text);
    
    public ITransferOperation CreateBitmapOperation(Bitmap bitmap)
    {
        var tempBitmapPath = GetTempBitmapPath();
        bitmap.Save(tempBitmapPath, ImageFormat.Png);

        _logger.LogInfo("Temporary bitmap file created, do not touch");
        _logger.LogDebug(tempBitmapPath);

        return CreateFileOperation(tempBitmapPath);
    }

    public ITransferOperation CreateFileOperation(params IEnumerable<string> files)
    {
        var preparedFiles = new List<string>();
        foreach (var plugin in _configHandler.LoadPlugins())
        {
            foreach (var file in files)
            {
                _logger.LogDebug($"Executing plugin {plugin} for {file}");
                var result = plugin.Execute(file);

                if (result.IsSuccess)
                {
                    _logger.LogDebug($"Plugin was successfully executed: {result.FilePath}");
                    preparedFiles.Add(result.FilePath);
                }
                else _logger.LogWarning($"Plugin could not be executed successfully: {result.ExitCode} - {result.ErrorMessage}");
            }
        }

        var tempZipPath = GetTempZipPath();
        CreateTempZip(tempZipPath, preparedFiles);

        var comPort = FindPicoPort(PicoVID, PicoPID);

        return comPort is null
            ? new NativeFileTransferOperation(_logger, _configHandler, tempZipPath)
            : new PicoFileTransferOperation(_logger, tempZipPath, comPort);
    }

    public void Dispose()
    {
        var path = GetTempZipPath();
        var bitmapPath = GetTempBitmapPath();

        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInfo("Temporary .zip file cleaned up");
        }

        if (File.Exists(bitmapPath))
        {
            File.Delete(path);
            _logger.LogInfo("Temporary bitmap file cleaned up");
        }

        GC.SuppressFinalize(this);
    }

    private unsafe string? FindPicoPort(string vid, string pid)
    {
        var comPort = Native.GUID_DEVINTERFACE_COMPORT;
        var deviceInfoSet = Native.SetupDiGetClassDevs(ref comPort, nint.Zero, nint.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == nint.Zero)
        {
            _logger.LogDebug(nameof(Native.SetupDiGetClassDevs), Native.TryGetError());
            return null;
        }

        try
        {
            var devInfo = new SP_DEVINFO_DATA
            {
                cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>()
            };

            Span<char> buffer = stackalloc char[byte.MaxValue + 1];
            Span<byte> rawBuffer = stackalloc byte[64];
            for (uint i = 0; Native.SetupDiEnumDeviceInfo(deviceInfoSet, i, ref devInfo); i++)
            {
                fixed (char* bufferPtr = buffer)
                {
                    if (!Native.SetupDiGetDeviceInstanceId(deviceInfoSet, ref devInfo, (nint)bufferPtr, buffer.Length * sizeof(char), out var requiredSize)) continue;

                    var instanceId = new string(bufferPtr);
                    if (!instanceId.Contains($"VID_{vid}", StringComparison.OrdinalIgnoreCase)
                        || !instanceId.Contains($"PID_{pid}", StringComparison.OrdinalIgnoreCase)) continue;
                }

                var hKey = Native.SetupDiOpenDevRegKey(deviceInfoSet, ref devInfo, Native.DICS_FLAG_GLOBAL, 0, Native.DIREG_DEV, Native.KEY_QUERY_VALUE);
                if (hKey == nint.Zero || hKey == -1) continue;

                try
                {
                    fixed (byte* rawBufferPtr = rawBuffer)
                    {
                        var size = (uint)rawBuffer.Length;
                        if (Native.RegQueryValueEx(hKey, "PortName", nint.Zero, out var type, rawBufferPtr, ref size) == 0
                            && type == Native.REG_SZ)
                        {
                            _logger.LogInfo($"A Pico device was found and will be used for transfer. This ignores {nameof(Config.TransferSecurity)}");
                            return Encoding.Unicode.GetString(rawBufferPtr, (int)size).TrimEnd(char.MinValue);
                        }
                    }
                }
                finally
                {
                    _ = Native.RegCloseKey(hKey);
                }
            }
        }
        finally
        {
            Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        _logger.LogDebug("No Pico device was found", Native.TryGetError());
        return null;
    }

    private void CreateTempZip(string tempZipPath, IEnumerable<string> files)
    {
        var entries = 0;
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
                                entries++;
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
                                    entries++;
                                }
                            }
                        }
                    }
                    else _logger.LogWarning($"The file or directory does not exist: {path}");
                }
            }
        }

        _logger.LogInfo($"Temporary .zip file created with {entries} entries, do not touch");
        _logger.LogDebug(tempZipPath);
    }

    private static string GetTempZipPath() => Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-TempFileTransfer.zip");
    private static string GetTempBitmapPath() => Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-Image.png");

    [GeneratedRegex(@"\((COM\d+)\)")]
    private static partial Regex ComPortRegex();
}