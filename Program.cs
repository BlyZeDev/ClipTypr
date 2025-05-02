namespace StrokeMyKeys;

using NotificationIcon.NET;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;

sealed class Program
{
    private const string RestartArgument = "/restarted";

    static void Main(string[] args)
    {
        var configurationHandler = new ConfigurationHandler();
        var consoleHandle = Native.GetConsoleWindow();

        if (!args.Contains(RestartArgument))
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

        Logger.LogInfo("Process has started");

        try
        {
            var startMenuHandler = new AutoStartHandler(consoleHandle);
            using (var trayIcon = new TrayIcon(Path.Combine(AppContext.BaseDirectory, "icon.ico")))
            {
                trayIcon.Show(
                [
                    new MenuItem("Write from clipboard")
                    {
                        IsChecked = null,
                        Click = (_, _) =>
                        {
                            if (configurationHandler.Current.IsFirstStart)
                            {
                                configurationHandler.Write(configurationHandler.Current with { IsFirstStart = false });

                                Native.ShowMessage(
                                    consoleHandle,
                                    "You have 3 seconds to focus on the text input into which the text is to be written",
                                    "Information",
                                    Native.MB_ICONEXLAMATION);
                            }

                            WriteFromClipboard();
                        }
                    },
                    new MenuItem("Show Logs")
                    {
                        IsChecked = Native.IsWindowVisible(consoleHandle),
                        Click = (sender, args) =>
                        {
                            var isVisible = Native.IsWindowVisible(consoleHandle);

                            ((MenuItem)sender!).IsChecked = !isVisible;

                            Native.ShowWindow(consoleHandle, isVisible ? Native.SW_HIDE : Native.SW_SHOW);
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
                                if (Native.ShowMessage(consoleHandle, "Restart?", "", Native.MB_ICONQUESTION | Native.MB_YESNO) == Native.IDYES)
                                    Restart(true);
                            }
                        }
                    },
                    new MenuItem("Autostart")
                    {
                        IsChecked = startMenuHandler.IsInStartMenu,
                        IsDisabled = !startMenuHandler.IsInitialized,
                        Click = (sender, args) =>
                        {
                            var menuItem = (MenuItem)sender!;
                            menuItem.IsChecked = !menuItem.IsChecked;
                            startMenuHandler.HandleStartMenu(menuItem.IsChecked ?? false);
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
        catch (Exception ex) { CloseGracefully(consoleHandle, ex); }

        Logger.LogInfo("Process has stopped");

        Environment.Exit(0);
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

    private static void WriteFromClipboard()
    {
        Logger.LogInfo("Trying to write from clipboard");

        var clipboardText = Clipboard.GetText();
        if (string.IsNullOrEmpty(clipboardText)) return;

        Logger.LogInfo("Selecting window to paste into, 3 sec");

        Thread.Sleep(3000);

        Logger.LogInfo($"Writing \"{clipboardText}\"");

        var simulator = new InputSimulator(clipboardText);
        simulator.SendInput();
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