namespace ClipTypr;

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Security.Principal;
using NotificationIcon.NET;
using ClipTypr.Common;
using ClipTypr.COM;
using ClipTypr.NATIVE;

public sealed class ServiceRunner : IDisposable
{
    private const string Version = "2.0.0";
    private const string RestartArgument = "/restarted";

    private readonly nint _consoleHandle;
    private readonly ConfigurationHandler _configHandler;
    private readonly string? _autostartPath;

    private ServiceRunner(nint consoleHandle)
    {
        _consoleHandle = consoleHandle;
        _configHandler = new ConfigurationHandler(config => Logger.LogLevel = config.LogLevel);
        _autostartPath = Environment.ProcessPath is null
            ? null
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), $"{Path.GetFileNameWithoutExtension(Environment.ProcessPath)}.lnk");

        Logger.LogDebug($"Process Path is {Environment.ProcessPath ?? "NULL"}");
        Logger.LogDebug($"Autostart Path is {_autostartPath ?? "NULL"}");
    }

    public void RunAndBlock()
    {
        try
        {
            using (var trayIcon = new TrayIcon(Path.Combine(AppContext.BaseDirectory, "icon.ico")))
            {
                trayIcon.Show(
                [
                    new MenuItem("Write Text from clipboard")
                    {
                        IsChecked = null,
                        Click = (_, _) => WriteFromClipboard(ClipboardFormat.UnicodeText)
                    },
                    new MenuItem("Write File from clipboard")
                    {
                        IsChecked = null,
                        Click = (_, _) => WriteFromClipboard(ClipboardFormat.File)
                    },
                    new MenuItem("Show Logs")
                    {
                        IsChecked = Native.IsWindowVisible(_consoleHandle),
                        Click = (sender, args) =>
                        {
                            var isVisible = Native.IsWindowVisible(_consoleHandle);

                            ((MenuItem)sender!).IsChecked = !isVisible;

                            Native.ShowWindow(_consoleHandle, isVisible ? Native.SW_HIDE : Native.SW_SHOW);
                        }
                    },
                    new MenuItem("Edit Configuration")
                    {
                        IsChecked = null,
                        Click = (_, _) =>
                        {
                            Logger.LogDebug("Opening the configuration file");

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
                    new MenuItem("Run as Admin")
                    {
                        IsChecked = IsRunAsAdmin(),
                        IsDisabled = IsRunAsAdmin(),
                        Click = (sender, args) =>
                        {
                            if (!IsRunAsAdmin())
                            {
                                var result = Native.ShowMessage(_consoleHandle, "Restart?", "", Native.MB_ICONQUESTION | Native.MB_YESNO);
                                if (result == Native.IDYES) Restart(true);
                            }
                        }
                    },
                    new MenuItem("Autostart")
                    {
                        IsChecked = File.Exists(_autostartPath),
                        IsDisabled = _autostartPath is null,
                        Click = (sender, args) =>
                        {
                            if (File.Exists(_autostartPath)) File.Delete(_autostartPath);
                            else Com.CreateShortcut(Environment.ProcessPath!, _autostartPath!, $"Launch {nameof(ClipTypr)}");

                            var isActivated = File.Exists(_autostartPath);
                            ((MenuItem)sender!).IsChecked = isActivated;

                            Logger.LogInfo($"Autostart is now {(isActivated ? "activated" : "removed")}");
                        }
                    },
                    new MenuItem($"Version {Version}")
                    {
                        IsChecked = false,
                        IsDisabled = true
                    },
                    new MenuItem("Exit")
                    {
                        IsChecked = null,
                        Click = (_, _) => trayIcon.Close()
                    }
                ]);

                Logger.LogDebug("The TrayIcon started blocking");
                trayIcon.BlockUntilExit();
                Logger.LogDebug("The TrayIcon stopped blocking");
            }
        }
        catch (Exception ex) { CloseGracefully(_consoleHandle, ex); }
    }

    [DoesNotReturn]
    public void Dispose()
    {
        _configHandler.Dispose();

        Logger.LogInfo("Process has stopped");
        Environment.Exit(0);
    }

    private void WriteFromClipboard(ClipboardFormat format)
    {
        Logger.LogInfo($"Trying to write {format} from clipboard");

        switch (format)
        {
            case ClipboardFormat.UnicodeText:
                var clipboardText = Clipboard.GetText();
                if (string.IsNullOrEmpty(clipboardText))
                {
                    Logger.LogInfo("No text in the clipboard");
                    return;
                }

                Logger.LogInfo($"Selecting window to paste into, {_configHandler.Current.PasteCooldownMs} milliseconds");

                Thread.Sleep(_configHandler.Current.PasteCooldownMs);

                Logger.LogInfo($"Writing \"{clipboardText}\"");

                InputSimulator.SendInput(clipboardText);
                break;

            case ClipboardFormat.File:
                var clipboardFile = Clipboard.GetFile();
                if (string.IsNullOrWhiteSpace(clipboardFile))
                {
                    Logger.LogInfo("No files in the clipboard");
                    return;
                }

                Logger.LogInfo($"Selecting window to paste into, {_configHandler.Current.PasteCooldownMs} milliseconds");

                Thread.Sleep(_configHandler.Current.PasteCooldownMs);

                Logger.LogInfo($"Writing \"{clipboardFile}\"");

                InputSimulator.SendFile(clipboardFile);
                break;
        }
    }

    public static ServiceRunner Initialize(in ReadOnlySpan<string?> arguments)
    {
        Logger.LogDebug($"Arguments: {(arguments.IsEmpty ? "<NULL>" : string.Join(',', arguments))}");
        
        var consoleHandle = Native.GetConsoleWindow();

        if (!arguments.Contains(RestartArgument))
        {
            if (Native.GetWindowLong(consoleHandle, Native.GWL_STYLE) <= 0)
            {
                try
                {
                    Restart(false);
                }
                catch (Exception ex) { CloseGracefully(consoleHandle, ex); }
            }
        }

        var stdOutHandle = Native.GetStdHandle(Native.STD_OUTPUT_HANDLE);

        Native.GetConsoleMode(stdOutHandle, out var mode);
        Native.SetConsoleMode(stdOutHandle, mode | Native.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        Native.ShowWindow(consoleHandle, Native.SW_HIDE);

        _ = Native.DeleteMenu(Native.GetSystemMenu(consoleHandle, false), Native.SC_CLOSE, 0);
        _ = Native.SetWindowLong(consoleHandle, Native.GWL_STYLE, Native.GetWindowLong(consoleHandle, Native.GWL_STYLE) & ~Native.WS_MINIMIZEBOX);

        Logger.LogInfo($"Process has started{(IsRunAsAdmin() ? " - Admin Mode" : "")}");

        return new ServiceRunner(consoleHandle);
    }

    [SuppressMessage("Interoperability", "CA1416:Plattformkompatibilität überprüfen", Justification = "<Ausstehend>")]
    private static bool IsRunAsAdmin()
    {
        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    [DoesNotReturn]
    private static void Restart(bool runAsAdmin)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "conhost.exe"),
            Arguments = $"{Environment.ProcessPath} {RestartArgument}" ?? throw new FileNotFoundException("The .exe path of the process couldn't be found"),
            UseShellExecute = true,
            Verb = runAsAdmin ? "runas" : ""
        });

        Environment.Exit(0);
    }

    [DoesNotReturn]
    private static void CloseGracefully(nint consoleHandle, Exception ex)
    {
        Logger.LogError("A fatal crash happened", ex);
        if (!Native.IsWindowVisible(consoleHandle))
        {
            Native.ShowWindow(consoleHandle, Native.SW_SHOW);
        }

        Console.Write("Press Enter to exit...");
        Console.ReadLine();

        Environment.Exit(0);
    }
}