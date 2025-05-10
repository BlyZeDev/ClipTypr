namespace StrokeMyKeys;

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Security.Principal;
using NotificationIcon.NET;

public sealed class ServiceRunner : IDisposable
{
    private const string RestartArgument = "/restarted";

    private readonly nint _consoleHandle;
    private readonly ConfigurationHandler _configHandler;

    private ServiceRunner(nint consoleHandle)
    {
        _consoleHandle = consoleHandle;
        _configHandler = new ConfigurationHandler();
    }

    public void RunAndBlock()
    {
        try
        {
            using (var trayIcon = new TrayIcon(Path.Combine(AppContext.BaseDirectory, "icon.ico")))
            {
                trayIcon.Show(
                [
                    new MenuItem("Write from clipboard")
                    {
                        IsChecked = null,
                        Click = (_, _) =>
                        {
                            if (_configHandler.Current.IsFirstStart)
                            {
                                _configHandler.Write(_configHandler.Current with { IsFirstStart = false });

                                Native.ShowMessage(
                                    _consoleHandle,
                                    "You have 3 seconds to focus on the text input into which the text is to be written",
                                    "Information",
                                    Native.MB_ICONEXLAMATION);
                            }

                            WriteFromClipboard();
                        }
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
                        IsDisabled = !_configHandler.HasAccess,
                        Click = (_, _) =>
                        {
                            
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
                                if (Native.ShowMessage(_consoleHandle, "Restart?", "", Native.MB_ICONQUESTION | Native.MB_YESNO) == Native.IDYES)
                                    Restart(true);
                            }
                        }
                    },
                    new MenuItem("Exit")
                    {
                        IsChecked = null,
                        Click = (_, _) => trayIcon.Close()
                    }
                ]);

                trayIcon.BlockUntilExit();
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

    private void WriteFromClipboard()
    {
        Logger.LogInfo("Trying to write from clipboard");

        var clipboardText = Clipboard.GetText();
        if (string.IsNullOrEmpty(clipboardText)) return;

        Logger.LogInfo($"Selecting window to paste into, {_configHandler.Current.PasteCooldownMs} milliseconds");

        Thread.Sleep(_configHandler.Current.PasteCooldownMs);

        Logger.LogInfo($"Writing \"{clipboardText}\"");

        var simulator = new InputSimulator(clipboardText);
        simulator.SendInput();
    }

    public static ServiceRunner Initialize(in ReadOnlySpan<string> arguments)
    {
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

        Logger.LogInfo($"Process has started - {(IsRunAsAdmin() ? "Admin Mode" : "")}");

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