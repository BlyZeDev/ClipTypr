namespace ClipTypr.Common;

using NotificationIcon.NET;
using ClipTypr.NATIVE;
using System.Drawing;

public sealed class TrayIcon : IDisposable
{
    private readonly string? _icoPath;
    private CancellationTokenSource? cts;

    public IReadOnlyList<MenuItem> MenuItems { get; private set; }

    public TrayIcon(string icoPath)
    {
        MenuItems = [];

        if (!Path.GetExtension(icoPath).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogError("The filepath needs an .ico extension, trying fallback icon", null);
            _icoPath = GetFallbackIco();
            return;
        }

        if (!File.Exists(icoPath))
        {
            Logger.LogError("The .ico file could not be found, trying fallback icon", null);
            _icoPath = GetFallbackIco();
            return;
        }

        Logger.LogDebug($"The .ico path is {icoPath}");

        _icoPath = icoPath;
    }

    public void Show(params IReadOnlyList<MenuItem> items)
    {
        if (_icoPath is null) return;

        Close();

        cts ??= new CancellationTokenSource();

        MenuItems = items;
        Task.Factory.StartNew(() =>
        {
            var notifyIcon = NotifyIcon.Create(_icoPath, MenuItems);
            notifyIcon.Show(cts?.Token ?? CancellationToken.None);
            notifyIcon.Dispose();
        }, cts?.Token ?? CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public void BlockUntilExit() => cts?.Token.WaitHandle.WaitOne();

    public void Close() => cts?.Cancel();

    public void Dispose()
    {
        Close();
        
        cts?.Dispose();
        cts = null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Plattformkompatibilität überprüfen", Justification = "<Ausstehend>")]
    private static string? GetFallbackIco()
    {
        const int ControlPanelIcon = 43;

        var sourceDll = Path.Combine(Environment.SystemDirectory, "shell32.dll");
        var hIcon = Native.ExtractIcon(nint.Zero, sourceDll, ControlPanelIcon);
        if (hIcon == nint.Zero)
        {
            Logger.LogWarning("Couldn't load a fallback icon", Native.GetError());
            return null;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Guid.CreateVersion7().ToString(), ".ico"));

        using (var icon = Icon.FromHandle(hIcon))
        {
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                icon.Save(fileStream);
                fileStream.Flush();
            }
        }

        return tempPath;
    }
}