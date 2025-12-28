namespace ClipTypr.Services;

using DotTray;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

public sealed class ServiceRunner : IDisposable
{
    private const int EntryLimit = 10;

    private readonly ILogger _logger;
    private readonly ClipTyprContext _context;
    private readonly ConsolePal _console;
    private readonly HotKeyHandler _hotkeyHandler;
    private readonly ConfigurationHandler _configHandler;
    private readonly ClipboardHandler _clipboard;
    private readonly InputSimulator _simulator;

    private readonly CancellationTokenSource _cts;
    private readonly NotifyIcon _notifyIcon;
    private readonly MenuItem _clipboardStoreItem;
    private readonly CircularHashQueue<ClipboardEntry> _clipboardStoreEntries;

    public ServiceRunner(ILogger logger, ClipTyprContext context, ConsolePal console, HotKeyHandler hotkeyHandler, ConfigurationHandler configHandler, ClipboardHandler clipboard, InputSimulator simulator)
    {
        _logger = logger;
        _context = context;
        _console = console;
        _hotkeyHandler = hotkeyHandler;
        _configHandler = configHandler;
        _clipboard = clipboard;
        _simulator = simulator;
        
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnhandledTaskException;

        _console.SetIcon(_context.IcoHandle);

        _clipboardStoreEntries = new CircularHashQueue<ClipboardEntry>(EntryLimit,
            EqualityComparer<ClipboardEntry>.Create((entry, other) => entry?.DisplayText == other?.DisplayText, entry => entry.DisplayText.GetHashCode()));

        _cts = new CancellationTokenSource();
        

        _notifyIcon = NotifyIcon.Run(_context.IcoHandle, _cts.Token);

        var currentItem = _notifyIcon.MenuItems.AddItem("Write from Clipboard");
        var submenuItem = currentItem.SubMenu.AddItem("Text");
        submenuItem.Clicked = _ => WriteFromClipboard(ClipboardFormat.UnicodeText, _configHandler.Current.PasteCooldownMs);
        submenuItem = currentItem.SubMenu.AddItem("Image");
        submenuItem.Clicked = _ => WriteFromClipboard(ClipboardFormat.DibV5, _configHandler.Current.PasteCooldownMs);
        submenuItem = currentItem.SubMenu.AddItem("Files");
        submenuItem.Clicked = _ => WriteFromClipboard(ClipboardFormat.Files, _configHandler.Current.PasteCooldownMs);

        _clipboardStoreItem = _notifyIcon.MenuItems.AddItem("Clipboard Store");

        currentItem = _notifyIcon.MenuItems.AddItem("Show Logs");
        currentItem.IsChecked = false;
        currentItem.Clicked = args =>
        {
            var isVisible = _console.IsVisible();

            args.MenuItem.IsChecked = !isVisible;

            if (isVisible) _console.HideWindow();
            else _console.ShowWindow();
        };

        currentItem = _notifyIcon.MenuItems.AddItem("Settings");
        submenuItem = currentItem.SubMenu.AddItem("Open Application Folder");
        submenuItem.Clicked = _ =>
        {
            _logger.LogDebug("Opening the application folder");
            OpenFile(_context.ApplicationDirectory);
        };
        submenuItem = currentItem.SubMenu.AddItem("Edit Configuration");
        submenuItem.Clicked = _ =>
        {
            _logger.LogDebug("Opening the configuration file");
            OpenFile(_context.ConfigurationPath);
        };
        submenuItem = currentItem.SubMenu.AddItem("Run as Admin");
        submenuItem.IsChecked = Util.IsRunAsAdmin();
        submenuItem.IsDisabled = Util.IsRunAsAdmin();
        submenuItem.Clicked = _ =>
        {
            if (Util.IsRunAsAdmin()) return;

            var shouldRestart = false;
            if (_console.SupportsModernDialog())
            {
                const string YesBtn = "Yes";

                var result = _console.ShowModernDialog("Restart", "Do you really want to restart?", null, null, null, YesBtn, "No");
                shouldRestart = result == YesBtn;
            }
            else
            {
                var result = _console.ShowDialog("Restart", "Do you really want to restart?", PInvoke.MB_ICONQUESTION | PInvoke.MB_YESNO);
                shouldRestart = result == PInvoke.IDYES;
            }

            if (shouldRestart) Restart(true);
        };
        submenuItem = currentItem.SubMenu.AddItem("Autostart");
        submenuItem.IsChecked = Util.IsInStartup(nameof(ClipTypr), _context.ExecutablePath);
        submenuItem.Clicked = args =>
        {
            if (Util.IsInStartup(nameof(ClipTypr), _context.ExecutablePath)) Util.RemoveFromStartup(nameof(ClipTypr));
            else Util.AddToStartup(nameof(ClipTypr), _context.ExecutablePath);

            var isActivated = Util.IsInStartup(nameof(ClipTypr), _context.ExecutablePath);
            args.MenuItem.IsChecked = isActivated;

            _logger.LogInfo($"Autostart is now {(isActivated ? "activated" : "removed")}");
        };

        _notifyIcon.MenuItems.AddSeparator();

        currentItem = _notifyIcon.MenuItems.AddItem($"{nameof(ClipTypr)} - Version {ClipTyprContext.Version}");
        currentItem.IsDisabled = true;
        currentItem.BackgroundDisabledColor = currentItem.BackgroundColor;
        currentItem.TextDisabledColor = currentItem.TextColor;

        _notifyIcon.MenuItems.AddSeparator();

        currentItem = _notifyIcon.MenuItems.AddItem("Exit");
        currentItem.Clicked = _ => _cts.Cancel();

        RefreshClipboardStoreSubMenu();
    }

    public async Task RunAsync()
    {
        _console.HideWindow();
        _console.SetTitle($"{nameof(ClipTypr)} - Logs");

        _logger.Log += OnLog;
        _configHandler.ConfigReload += OnConfigReload;

        _hotkeyHandler.RegisterHotKey(_configHandler.Current.PasteHotKey);

        _hotkeyHandler.HotKeyPressed += OnHotKeyPressed;
        _clipboard.ClipboardUpdate += OnClipboardUpdated;

        OnConfigReload(this, new ConfigChangedEventArgs
        {
            OldConfig = _configHandler.Current,
            NewConfig = _configHandler.Current
        });

        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _clipboard.ClipboardUpdate -= OnClipboardUpdated;
        _configHandler.ConfigReload -= OnConfigReload;
        _hotkeyHandler.HotKeyPressed -= OnHotKeyPressed;
        _logger.Log -= OnLog;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnhandledTaskException;

        _notifyIcon.Dispose();

        _logger.LogInfo($"{nameof(ClipTypr)} has stopped");

        GC.SuppressFinalize(this);
    }

    private void RefreshClipboardStoreSubMenu()
    {
        _clipboardStoreItem.SubMenu.Clear();

        var currentItem = _clipboardStoreItem.SubMenu.AddItem("Add Entry");
        currentItem.Clicked = _ =>
        {
            var format = _clipboard.GetCurrentFormat();
            AddClipboardEntry(format);
        };

        if (_clipboardStoreEntries.Count > 0) _clipboardStoreItem.SubMenu.AddSeparator();

        foreach (var entry in _clipboardStoreEntries)
        {
            currentItem = _clipboardStoreItem.SubMenu.AddItem(entry.DisplayText);
            currentItem.Clicked = _ =>
            {
                switch (entry)
                {
                    case TextClipboardEntry text: _clipboard.SetText(text.Text); break;
                    case ImageClipboardEntry image: _clipboard.SetBitmap(image.Image); break;
                    case FilesClipboardEntry files: _clipboard.SetFiles(files.Files); break;
                }
            };
        }

        if (_clipboardStoreEntries.Count > 0)
        {
            _clipboardStoreItem.SubMenu.AddSeparator();
            currentItem = _clipboardStoreItem.SubMenu.AddItem("Clear Entries");
            currentItem.Clicked = _ =>
            {
                _clipboardStoreEntries.Clear();
                _logger.LogInfo("Cleared all entries");
                RefreshClipboardStoreSubMenu();
            };
        }
    }

    private void WriteFromClipboard(ClipboardFormat format, int cooldownMs)
    {
        _logger.LogInfo($"Trying to write {format} from clipboard");

        switch (format)
        {
            case ClipboardFormat.UnicodeText:
                {
                    var clipboardText = _clipboard.GetText();
                    if (string.IsNullOrEmpty(clipboardText))
                    {
                        _logger.LogInfo("No text in the clipboard");
                        return;
                    }

                    _logger.LogInfo($"Select window to paste into, you have {cooldownMs} milliseconds");

                    Thread.Sleep(cooldownMs);

                    _simulator.CreateTextOperation(clipboardText).Send();
                }
                break;

            case ClipboardFormat.DibV5:
                {
                    var clipboardBitmap = _clipboard.GetBitmap();
                    if (clipboardBitmap is null || clipboardBitmap.Size.IsEmpty)
                    {
                        _logger.LogInfo("No image in the clipboard");
                        return;
                    }

                    HandleZipOperation(_simulator.CreateBitmapOperation(clipboardBitmap), cooldownMs);
                }
                break;

            case ClipboardFormat.Files:
                {
                    var clipboardFiles = _clipboard.GetFiles();
                    if (clipboardFiles.Count == 0)
                    {
                        _logger.LogInfo("No files in the clipboard");
                        return;
                    }

                    HandleZipOperation(_simulator.CreateFileOperation(clipboardFiles), cooldownMs);
                }
                break;

            default: _logger.LogError("This format is not supported", null); break;
        }

        
    }

    private void HandleZipOperation(ITransferOperation operation, int cooldownMs)
    {
        var shouldTransfer = false;
        if (_console.SupportsModernDialog())
        {
            const string YesBtn = "Start Transferring";

            var result = _console.ShowModernDialog(
                "Confirmation",
                $"The estimated runtime is about {Util.FormatTime(operation.EstimatedRuntime) ?? "Unknown"}",
                """
                    The computer is not usable while transferring!

                    The operation will abort if the focus is changed.

                    Are you sure you want to start pasting the file?
                    """,
                null, null, YesBtn, "Abort");
            shouldTransfer = result == YesBtn;
        }
        else
        {
            var result = _console.ShowDialog(
                "Confirmation",
                $"""
                    The computer is not usable while transferring!
                    
                    The operation will abort if the focus is changed.
                    
                    The estimated transfer time is about {Util.FormatTime(operation.EstimatedRuntime) ?? "Unknown"}
                    
                    Are you sure you want to start pasting the file?
                    """,
                PInvoke.MB_ICONEXLAMATION | PInvoke.MB_YESNO);
            shouldTransfer = result == PInvoke.IDYES;
        }

        if (!shouldTransfer)
        {
            _logger.LogInfo("The file transfer was aborted");
            return;
        }

        _logger.LogInfo($"Select window to paste into, you have {cooldownMs} milliseconds");

        Thread.Sleep(cooldownMs);

        operation.Send();
    }

    private void OnLog(LogLevel logLevel, string message, Exception? exception)
    {
        if (logLevel is not LogLevel.Error) return;

        _notifyIcon.ShowBalloon(new BalloonNotification
        {
            Title = exception is null ? "An error occurred" : message,
            Message = exception is null ? message : exception.ToString(),
            Icon = BalloonNotificationIcon.Error
        });
    }

    private void OnHotKeyPressed(object? sender, HotKey hotkey)
    {
        _logger.LogDebug($"Pressed hotkey: {hotkey}");

        if (hotkey == _configHandler.Current.PasteHotKey) WriteFromClipboard(ClipboardFormat.UnicodeText, 100);
    }

    private void OnConfigReload(object? sender, ConfigChangedEventArgs args)
    {
        _logger.LogLevel = _configHandler.Current.LogLevel;

        if (args.OldConfig.PasteHotKey == args.NewConfig.PasteHotKey) return;

        _hotkeyHandler.UnregisterHotKey(args.OldConfig.PasteHotKey);
        _hotkeyHandler.RegisterHotKey(args.NewConfig.PasteHotKey);
    }

    private void OnClipboardUpdated()
    {
        if (!_configHandler.Current.AutoStore) return;

        var format = _clipboard.GetCurrentFormat();
        AddClipboardEntry(format);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception) ControlledCrash(exception);
        else ControlledCrash(new Exception("Unknown exception was thrown"));
    }

    private void OnUnhandledTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        args.SetObserved();
        ControlledCrash(args.Exception);
    }

    [DoesNotReturn]
    private void Restart(bool runAsAdmin)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _context.ExecutablePath,
            UseShellExecute = true,
            Verb = runAsAdmin ? "runas" : ""
        });

        Environment.Exit(0);
    }

    [DoesNotReturn]
    private void ControlledCrash(Exception exception)
    {
        _logger.LogCritical("The application crashed", exception);

        if (!_console.IsVisible()) _console.ShowWindow();

        var crashLogPath = _context.WriteCrashLog(exception);

        var isHandled = true;
        if (_console.SupportsModernDialog())
        {
            const string OkayBtn = "Okay";
            const string ReportBtn = "Report";
            const string ViewCrashLogBtn = "View Crash Log";

            var result = _console.ShowModernDialog(
                "A fatal error occured",
                "The application crashed.",
                $"The detailed Crash Log can be found here:\n{crashLogPath}.",
                exception.ToString(),
                null,
                OkayBtn, ReportBtn, ViewCrashLogBtn);

            switch (result)
            {
                case OkayBtn: break;
                case ReportBtn: OpenGitHubIssue(exception.Message, exception.StackTrace ?? "No Stack Trace available"); break;
                case ViewCrashLogBtn: OpenFile(crashLogPath); break;
                default: isHandled = false; break;
            }
        }

        if (!isHandled)
        {
            _ = _console.ShowDialog(
                "A fatal error occured",
                $"""
                The application crashed.

                The detailed Crash Log can be found here: {crashLogPath}.

                To report the crash click the Help button.
                """,
                PInvoke.MB_ICONERROR,
                _ => OpenGitHubIssue(exception.Message, exception.StackTrace ?? "No Stack Trace available"));
        }

        Environment.FailFast(exception.Message, exception);
    }

    private void OpenGitHubIssue(string message, string stackTrace)
    {
        _clipboard.SetText($"```cs\n{Util.RedactUsername(stackTrace)}\n```");

        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = $"https://github.com/BlyZeDev/{nameof(ClipTypr)}/issues/new?template=issue.yaml&title={message}&version={ClipTyprContext.Version}",
                UseShellExecute = true
            };
            process.Start();
        }
    }

    private void AddClipboardEntry(ClipboardFormat format)
    {
        ClipboardEntry? clipboardEntry = format switch
        {
            ClipboardFormat.UnicodeText when _clipboard.GetText() is string text => new TextClipboardEntry(text),
            ClipboardFormat.DibV5 when _clipboard.GetBitmap() is Bitmap bitmap => new ImageClipboardEntry(bitmap),
            ClipboardFormat.Files when _clipboard.GetFiles() is { Count: > 0 } files => new FilesClipboardEntry(files),
            _ => null
        };

        if (clipboardEntry is null)
        {
            _logger.LogWarning("No clipboard entry could be created");
            return;
        }   

        var result = _clipboardStoreEntries.Enqueue(clipboardEntry);

        if (result is EnqueueResult.RemovedOldestEntry) _logger.LogInfo($"Removed the oldest entry because the entry limit of {_clipboardStoreEntries.Capacity} was reached");

        _logger.LogInfo($"Added {clipboardEntry.GetType().Name} to the entries");

        RefreshClipboardStoreSubMenu();
    }

    private static void OpenFile(string filepath)
    {
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = filepath
            };
            process.Start();
        }
    }
}