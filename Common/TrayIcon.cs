namespace StrokeMyKeys.Common;

using NotificationIcon.NET;

public sealed class TrayIcon : IDisposable
{
    private readonly string? _icoPath;
    private CancellationTokenSource? cts;

    public TrayIcon(string icoPath)
    {
        if (!Path.GetExtension(icoPath).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            var exception = new ArgumentException("The filepath needs an .ico extension");
            Logger.LogError(exception.Message, exception);
            return;
        }

        if (!File.Exists(icoPath))
        {
            var exception = new FileNotFoundException("The .ico file could not be found", icoPath);
            Logger.LogError(exception.Message, exception);
            return;
        }

        _icoPath = icoPath;
    }

    public void Show(params IReadOnlyList<MenuItem> items)
    {
        if (_icoPath is null) return;

        Close();

        cts ??= new CancellationTokenSource();

        Task.Factory.StartNew(() =>
        {
            var notifyIcon = NotifyIcon.Create(_icoPath, items);
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
}