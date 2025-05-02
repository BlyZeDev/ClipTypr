namespace StrokeMyKeys;

using NotificationIcon.NET;
using System.Text;

sealed class Program
{
    static void Main()
    {
        var configurationPath = Path.Combine(AppContext.BaseDirectory, "appdata.config");

        var consoleHandle = Native.GetConsoleWindow();
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
                            if (IsFirstStart(configurationPath))
                            {
                                WriteFirstStart(configurationPath, false);
                                Native.ShowMessage(
                                    consoleHandle,
                                    "You have 3 seconds to focus on the text input into which the text is to be written",
                                    "Hinweis",
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

                            var menuItem = (MenuItem)sender!;
                            menuItem.IsChecked = !isVisible;
                            Native.ShowWindow(consoleHandle, isVisible ? Native.SW_HIDE : Native.SW_SHOW);
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
        catch (Exception ex) { Logger.LogError(ex.Message, ex); }

        Logger.LogInfo("Process has stopped");

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

    private static bool IsFirstStart(string filepath)
    {
        if (!File.Exists(filepath)) WriteFirstStart(filepath, true);

        using (var fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read))
        {
            using (var reader = new BinaryReader(fileStream, Encoding.UTF8, true))
            {
                return reader.ReadBoolean();
            }
        }
    }

    private static void WriteFirstStart(string filepath, bool isFirstStart)
    {
        using (var fileStream = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.Write))
        {
            using (var writer = new BinaryWriter(fileStream, Encoding.UTF8, true))
            {
                writer.Write(isFirstStart);
                writer.Flush();
            }

            fileStream.Flush();
        }
    }
}