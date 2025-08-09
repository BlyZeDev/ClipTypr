namespace ClipTypr.Services;

using DotTray;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;

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
    private readonly Queue<ClipboardEntry> _clipboardStoreEntries;

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

        _clipboardStoreEntries = [];

        _cts = new CancellationTokenSource();
        var menuItems = new MenuItemCollection(
        [
            new MenuItem
            {
                Text = "Write from Clipboard",
                IsChecked = null,
                IsDisabled = false,
                SubMenu =
                [
                    new MenuItem
                    {
                        Text = "Text",
                        IsChecked = null,
                        Click = (_, _) => WriteFromClipboard(ClipboardFormat.UnicodeText, _configHandler.Current.PasteCooldownMs)
                    },
                    new MenuItem
                    {
                        Text = "Image",
                        IsChecked = null,
                        Click = (_, _) => WriteFromClipboard(ClipboardFormat.DibV5, _configHandler.Current.PasteCooldownMs)
                    },
                    new MenuItem
                    {
                        Text = "Files",
                        IsChecked = null,
                        Click = (_, _) => WriteFromClipboard(ClipboardFormat.Files, _configHandler.Current.PasteCooldownMs)
                    },
                ]
            },
            _clipboardStoreItem = new MenuItem
            {
                Text = "Clipboard Store",
                IsChecked = null,
                IsDisabled = false
            },
            new MenuItem
            {
                Text = "Show Logs",
                IsChecked = false,
                IsDisabled = false,
                Click = (sender, args) =>
                {
                    var isVisible = _console.IsVisible();

                    sender.IsChecked = !isVisible;

                    if (isVisible) _console.HideWindow();
                    else _console.ShowWindow();
                }
            },
            new MenuItem
            {
                Text = "Settings",
                IsChecked = null,
                IsDisabled = false,
                SubMenu =
                [
                    new MenuItem
                    {
                        Text = "Plugin Script Tester",
                        IsChecked = null,
                        IsDisabled = false,
                        Click = (_, _) =>
                        {
                            var fileDialogResult = Util.OpenFileDialog(
                                "Open a script file",
                                _context.PluginDirectory,
                                $"Lua Script (*{LuaPlugin.FileExtension})\0*{LuaPlugin.FileExtension}\0Powershell Script (*{PowershellPlugin.FileExtension})\0*{PowershellPlugin.FileExtension}\0");

                            var plugin = _configHandler.LoadPlugin(fileDialogResult);
                            if (plugin is null)
                            {
                                _logger.LogWarning($"No script could be loaded from {fileDialogResult}");
                                return;
                            }

                            ReadOnlySpan<string> testFilePaths = [_context.GetTempPath(".png"), _context.GetTempPath(".zip")];

                            var resultBuilder = new StringBuilder();
                            foreach (var testFilePath in testFilePaths)
                            {
                                _logger.LogDebug($"Executing test for {testFilePath}");

                                var currentResult = plugin.Execute(testFilePath);

                                resultBuilder.AppendLine($"# Result: {(currentResult.IsSuccess ? "Success" : "Fail")}");
                                resultBuilder.AppendLine();
                                resultBuilder.AppendLine("### Input");
                                resultBuilder.AppendLine($"`{testFilePath}`");
                                resultBuilder.AppendLine();
                                resultBuilder.AppendLine("### Output");
                                resultBuilder.AppendLine($"{(currentResult.IsSuccess ? $"`{currentResult.FilePath}`" : $"```cs\n{currentResult.Error}\n```")}");
                                resultBuilder.AppendLine();
                                resultBuilder.AppendLine();
                            }

                            var resultPath = _context.GetTempPath(".md");
                            using (var fileStream = new FileStream(resultPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                            {
                                using (var writer = new StreamWriter(fileStream, Encoding.Unicode, -1, true))
                                {
                                    writer.Write(resultBuilder.ToString());
                                }

                                fileStream.Flush();
                            }

                            _logger.LogDebug("Opening the temporary test results");
                            OpenFile(resultPath);
                        }
                    },
                    new MenuItem
                    {
                        Text = "Open Application Folder",
                        IsChecked = null,
                        IsDisabled = false,
                        Click = (_, _) =>
                        {
                            _logger.LogDebug("Opening the application folder");
                            OpenFile(_context.AppFilesDirectory);
                        }
                    },
                    new MenuItem
                    {
                        Text = "Edit Configuration",
                        IsChecked = null,
                        IsDisabled = false,
                        Click = (_, _) =>
                        {
                            _logger.LogDebug("Opening the configuration file");
                            OpenFile(_context.ConfigurationPath);
                        }
                    },
                    new MenuItem
                    {
                        Text = "Run as Admin",
                        IsChecked = Util.IsRunAsAdmin(),
                        IsDisabled = Util.IsRunAsAdmin(),
                        Click = (sender, args) =>
                        {
                            if (!Util.IsRunAsAdmin())
                            {
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
                            }
                        }
                    },
                    new MenuItem
                    {
                        Text = "Autostart",
                        IsChecked = Util.IsInStartup(nameof(ClipTypr), _context.ExecutablePath),
                        IsDisabled = false,
                        Click = (sender, args) =>
                        {
                            if (Util.IsInStartup(nameof(ClipTypr), _context.ExecutablePath)) Util.RemoveFromStartup(nameof(ClipTypr));
                            else Util.AddToStartup(nameof(ClipTypr), _context.ExecutablePath);

                            var isActivated = Util.IsInStartup(nameof(ClipTypr), _context.ExecutablePath);
                            sender.IsChecked = isActivated;

                            _logger.LogInfo($"Autostart is now {(isActivated ? "activated" : "removed")}");
                        }
                    },
                ]
            },
            SeparatorItem.Instance,
            new MenuItem
            {
                Text = $"{nameof(ClipTypr)} - Version {ClipTyprContext.Version}",
                IsChecked = null,
                IsDisabled = true
            },
            SeparatorItem.Instance,
            new MenuItem
            {
                Text = "Exit",
                IsChecked = null,
                IsDisabled = false,
                Click = (_, _) => _cts.Cancel()
            }
        ]);

        _notifyIcon = NotifyIcon.Run(_context.IcoHandle, menuItems, _cts.Token);
        RefreshClipboardStoreSubMenu();
    }

    public async Task RunAsync()
    {
        _console.HideWindow();
        _console.SetTitle($"{nameof(ClipTypr)} - Logs");

        _logger.Log += OnLog;
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
        _logger.Log -= OnLog;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnhandledTaskException;

        _notifyIcon.Dispose();

        _logger.LogInfo($"{nameof(ClipTypr)} has stopped");
    }

    private void RefreshClipboardStoreSubMenu()
    {
        _clipboardStoreItem.SubMenu.Clear();

        _clipboardStoreItem.SubMenu.Add(new MenuItem
        {
            Text = "Add Entry",
            Click = (_, _) =>
            {
                var format = _clipboard.GetCurrentFormat();
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

                if (_clipboardStoreEntries.Count >= EntryLimit)
                {
                    var dequeued = _clipboardStoreEntries.Dequeue();
                    _logger.LogInfo($"Removed {dequeued.DisplayText} because the entry limit of {EntryLimit} was reached");
                }

                _clipboardStoreEntries.Enqueue(clipboardEntry);
                _logger.LogInfo($"Added {clipboardEntry.DisplayText} to the entries");

                RefreshClipboardStoreSubMenu();
            }
        });

        if (_clipboardStoreEntries.Count > 0) _clipboardStoreItem.SubMenu.Add(SeparatorItem.Instance);

        foreach (var entry in _clipboardStoreEntries)
        {
            _clipboardStoreItem.SubMenu.Add(new MenuItem
            {
                Text = entry.DisplayText,
                Click = (_, _) =>
                {
                    switch (entry)
                    {
                        case TextClipboardEntry text: _clipboard.SetText(text.Text); break;
                        case ImageClipboardEntry image: _clipboard.SetBitmap(image.Image); break;
                        case FilesClipboardEntry files: _clipboard.SetFiles(files.Files); break;
                    }
                }
            });
        }

        if (_clipboardStoreEntries.Count > 0)
        {
            _clipboardStoreItem.SubMenu.Add(SeparatorItem.Instance);
            _clipboardStoreItem.SubMenu.Add(new MenuItem
            {
                Text = "Clear Entries",
                Click = (_, _) =>
                {
                    _clipboardStoreEntries.Clear();
                    _logger.LogInfo("Cleared all entries");
                    RefreshClipboardStoreSubMenu();
                }
            });
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