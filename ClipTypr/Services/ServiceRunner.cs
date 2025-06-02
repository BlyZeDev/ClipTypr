namespace ClipTypr.Services;

using Microsoft.Win32;
using NotificationIcon.NET;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

public sealed class ServiceRunner : IDisposable
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string Version = "2.1.0";

    private readonly ILogger _logger;
    private readonly ConsolePal _console;
    private readonly HotKeyHandler _hotkeyHandler;
    private readonly ConfigurationHandler _configHandler;
    private readonly ClipboardService _clipboard;
    private readonly InputSimulator _simulator;
    private readonly KeyboardTranslator _translator;

    private readonly CancellationTokenSource _cts;
    private readonly Thread _trayIconThread;
    private readonly MenuItem[] _menuItems;

    public ServiceRunner(ILogger logger, ConsolePal console, HotKeyHandler hotkeyHandler, ConfigurationHandler configHandler, ClipboardService clipboard, InputSimulator simulator, KeyboardTranslator translator)
    {
        _logger = logger;
        _console = console;
        _hotkeyHandler = hotkeyHandler;
        _configHandler = configHandler;
        _clipboard = clipboard;
        _simulator = simulator;
        _translator = translator;

        _logger.LogDebug($"Process Path is {Environment.ProcessPath ?? "NULL"}");
        
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnhandledTaskException;

        var icoPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (!File.Exists(icoPath)) icoPath = GetFallbackIco();
        if (icoPath is null) throw new MissingIconException("No icon could be found");
        
        _logger.LogDebug($"The .ico path is {icoPath}");

        _cts = new CancellationTokenSource();

        _menuItems =
        [
            new MenuItem("Write Text from Clipboard")
            {
                IsChecked = null,
                Click = (_, _) => WriteFromClipboard(ClipboardFormat.UnicodeText, (int)_configHandler.Current.PasteCooldownMs)
            },
            new MenuItem("Write Image from Clipboard")
            {
                IsChecked = null,
                Click = (_, _) => WriteFromClipboard(ClipboardFormat.Bitmap, (int)_configHandler.Current.PasteCooldownMs)
            },
            new MenuItem("Write File from Clipboard")
            {
                IsChecked = null,
                Click = (_, _) => WriteFromClipboard(ClipboardFormat.Files, (int)_configHandler.Current.PasteCooldownMs)
            },
            new MenuItem("Keyboard Translation")
            {
                IsChecked = _translator.IsTranslating,
                Click = (sender, args) =>
                {
                    if (_translator.IsTranslating) _translator.Stop();
                    else _translator.Start();

                    ((MenuItem)sender!).IsChecked = _translator.IsTranslating;
                }
            },
            new MenuItem("Edit Configuration")
            {
                IsChecked = null,
                Click = (_, _) =>
                {
                    _logger.LogDebug("Opening the configuration file");

                    using (var process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName = _configHandler.ConfigPath,
                            UseShellExecute = true
                        };
                        process.Start();
                    }
                }
            },
            new MenuItem("Show Logs")
            {
                IsChecked = false,
                Click = (sender, args) =>
                {
                    var isVisible = _console.IsVisible();

                    ((MenuItem)sender!).IsChecked = !isVisible;

                    if (isVisible) _console.HideWindow();
                    else _console.ShowWindow();
                }
            },
            new MenuItem("Run as Admin")
            {
                IsChecked = Util.IsRunAsAdmin(),
                IsDisabled = Util.IsRunAsAdmin(),
                Click = (sender, args) =>
                {
                    if (!Util.IsRunAsAdmin())
                    {
                        var answer = _console.ShowDialog("Restart", "Do you really want to restart?", Native.MB_ICONQUESTION | Native.MB_YESNO);
                        if (answer == Native.IDYES) Restart(true);
                    }
                }
            },
            new MenuItem("Autostart")
            {
                IsChecked = IsInStartup(Environment.ProcessPath ?? ""),
                IsDisabled = Environment.ProcessPath is null,
                Click = (sender, args) =>
                {
                    if (Environment.ProcessPath is null) return;

                    if (IsInStartup(Environment.ProcessPath)) RemoveFromStartup();
                    else AddToStartup(Environment.ProcessPath);

                    var isActivated = IsInStartup(Environment.ProcessPath);
                    ((MenuItem)sender!).IsChecked = isActivated;

                    _logger.LogInfo($"Autostart is now {(isActivated ? "activated" : "removed")}");
                }
            },
            new MenuItem($"{nameof(ClipTypr)} - Version {Version}")
            {
                IsChecked = false,
                IsDisabled = true
            },
            new MenuItem("Exit")
            {
                IsChecked = null,
                Click = (_, _) => _cts.Cancel()
            }
        ];
        _trayIconThread = new Thread(() =>
        {
            using (var notifyIcon = NotifyIcon.Create(icoPath, _menuItems))
            {
                notifyIcon.Show(_cts.Token);
            }
        });
        _trayIconThread.Start();
    }

    public async Task RunAsync()
    {
        _console.HideWindow();
        _console.SetTitle($"{nameof(ClipTypr)} - Logs");

        _logger.LogInfo($"Service has started{(Util.IsRunAsAdmin() ? " in Admin Mode" : "")}");

        _configHandler.ConfigReload += OnConfigReload;

        if (_hotkeyHandler.IsReady) OnHotKeysReady(this, EventArgs.Empty);
        else _hotkeyHandler.Ready += OnHotKeysReady;

        _hotkeyHandler.HotKeyPressed += OnHotKeyPressed;

        OnConfigReload(this, new ConfigChangedEventArgs
        {
            OldConfig = _configHandler.Current,
            NewConfig = _configHandler.Current
        });

        try
        {
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (TaskCanceledException) { }
    }

    public void Dispose()
    {
        _configHandler.ConfigReload -= OnConfigReload;
        _hotkeyHandler.Ready -= OnHotKeysReady;
        _hotkeyHandler.HotKeyPressed -= OnHotKeyPressed;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnhandledTaskException;

        if (_trayIconThread.IsAlive) _trayIconThread.Join();

        _logger.LogInfo("Service has stopped");
    }

    private void WriteFromClipboard(ClipboardFormat format, int cooldownMs)
    {
        _logger.LogInfo($"Trying to write {format} from clipboard");

        switch (format)
        {
            case ClipboardFormat.UnicodeText:
                var clipboardText = _clipboard.GetText();
                if (string.IsNullOrEmpty(clipboardText))
                {
                    _logger.LogInfo("No text in the clipboard");
                    return;
                }

                _logger.LogInfo($"Select window to paste into, you have {cooldownMs} milliseconds");

                Thread.Sleep(cooldownMs);

                _simulator.CreateTextOperation(clipboardText).Send();
                break;

            case ClipboardFormat.Bitmap:
                var clipboardBitmap = _clipboard.GetBitmap();
                if (clipboardBitmap is null || clipboardBitmap.Size.IsEmpty)
                {
                    _logger.LogInfo("No image in the clipboard");
                    return;
                }

                HandleZipOperation(_simulator.CreateBitmapOperation(clipboardBitmap), 1);
                break;

            case ClipboardFormat.Files:
                var clipboardFiles = _clipboard.GetFiles();
                if (clipboardFiles.Count == 0)
                {
                    _logger.LogInfo("No files in the clipboard");
                    return;
                }

                HandleZipOperation(_simulator.CreateFileOperation(clipboardFiles), clipboardFiles.Count);
                break;
        }

        void HandleZipOperation(ITransferOperation operation, int fileCount)
        {
            var answer = _console.ShowDialog(
                "Confirmation",
                $"""
                The computer is not usable while transferring!
                    
                The operation will abort if the focus is changed.
                    
                The estimated transfer time is about {Util.FormatTime(operation.EstimatedRuntime) ?? "Unknown"}
                    
                Are you sure you want to start pasting the file?
                """,
                Native.MB_ICONEXLAMATION | Native.MB_YESNO);
            if (answer != Native.IDYES) return;

            _logger.LogInfo($"Select window to paste into, you have {cooldownMs} milliseconds");

            Thread.Sleep(cooldownMs);

            operation.Send();
        }
    }

    private void OnHotKeysReady(object? sender, EventArgs e) => _hotkeyHandler.RegisterHotKey(_configHandler.Current.PasteHotKey);

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

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception) CloseGracefully(exception);
        else CloseGracefully(new Exception("Unknown exception was thrown"));
    }

    private void OnUnhandledTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        args.SetObserved();
        CloseGracefully(args.Exception);
    }

    [DoesNotReturn]
    private void CloseGracefully(Exception ex)
    {
        _logger.LogCritical("A fatal crash happened", ex);

        if (!_console.IsVisible()) _console.ShowWindow();

        _ = _console.ShowDialog(
            "A fatal error occured",
            """
            The application crashed.

            If you want to report this issue click the Help button.
            The error information will be copied into the clipboard, so you can paste it into the 'Error' field.

            Don't close this Dialog before pasting the error, otherwise the clipboard will be lost.
            """,
            Native.MB_ICONERROR,
            helpInfo => OpenGitHubIssue(ex.Message, ex.StackTrace ?? "No Stack Trace available"));

        Environment.FailFast(ex.Message, ex);
    }

    private void OpenGitHubIssue(string message, string stackTrace)
    {
        _clipboard.SetText($"```cs\n{Util.RedactUsername(stackTrace)}\n```");

        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo
            {
                FileName = $"https://github.com/BlyZeDev/{nameof(ClipTypr)}/issues/new?template=issue.yaml&title={message}&version={Version}",
                UseShellExecute = true
            };
            process.Start();
        }
    }

    private bool IsInStartup(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey);
        if (key is null)
        {
            _logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return false;
        }

        var value = key.GetValue(nameof(ClipTypr))?.ToString();
        return value is not null && executablePath.Equals(value.Trim('\"'), StringComparison.OrdinalIgnoreCase);
    }

    private void AddToStartup(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key is null)
        {
            _logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return;
        }

        key.SetValue(nameof(ClipTypr), $"\"{executablePath}\"");
    }

    private void RemoveFromStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key is null)
        {
            _logger.LogWarning($"Could not open registry key: {StartupRegistryKey}");
            return;
        }

        key.DeleteValue(nameof(ClipTypr));
    }

    [DoesNotReturn]
    private static void Restart(bool runAsAdmin)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? throw new FileNotFoundException("The .exe path of the process couldn't be found"),
            UseShellExecute = true,
            Verb = runAsAdmin ? "runas" : ""
        });

        Environment.Exit(0);
    }

    private static string? GetFallbackIco()
    {
        const int ControlPanelIcon = 43;

        var hIcon = Native.ExtractIcon(nint.Zero, Path.Combine(Environment.SystemDirectory, "shell32.dll"), ControlPanelIcon);
        if (hIcon == nint.Zero) return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"{nameof(ClipTypr)}-Fallback.ico");

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