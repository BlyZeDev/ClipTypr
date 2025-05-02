namespace StrokeMyKeys;

public sealed class AutoStartHandler
{
    private readonly nint _consoleHandle;
    private readonly string _batPath = null!;
    private readonly string _batContent = null!;

    public bool IsInitialized { get; }

    public bool IsInStartMenu => IsInitialized && File.Exists(_batPath);

    public AutoStartHandler(nint consoleHandle)
    {
        _consoleHandle = consoleHandle;

        var exePath = Environment.ProcessPath;
        if (!File.Exists(exePath))
        {
            IsInitialized = false;
            Logger.LogWarning("Couldn't find the process executable");
            return;
        }

        IsInitialized = true;
        _batPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), $"{Path.GetFileNameWithoutExtension(exePath)}.bat");
        _batContent = $"start \"\" \"{exePath}\"";
    }

    public void HandleStartMenu(bool shouldAdd)
    {
        if (!IsInitialized) return;

        if (shouldAdd) TryAddToStartMenu();
        else RemoveFromStartMenu();
    }

    private void TryAddToStartMenu()
    {
        if (File.Exists(_batPath))
        {
            Logger.LogInfo("Already in Autostart, did nothing");
            return;
        }

        using (var writer = new StreamWriter(_batPath))
        {
            writer.Write(_batContent);
            writer.Flush();
        }

        Logger.LogInfo("Added to Autostart");
        Native.ShowMessage(_consoleHandle, "Successfully added to autostart", "Success", Native.MB_ICONINFORMATION);
    }

    private void RemoveFromStartMenu()
    {
        if (!File.Exists(_batPath))
        {
            Logger.LogInfo("Not in Autostart, did nothing");
            return;
        }

        File.Delete(_batPath);
        Logger.LogInfo("Removed from Autostart");
        Native.ShowMessage(_consoleHandle, "Successfully removed from autostart", "Success", Native.MB_ICONINFORMATION);
    }
}