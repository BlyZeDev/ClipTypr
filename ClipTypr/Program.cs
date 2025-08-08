namespace ClipTypr;

sealed class Program
{
    static void Main()
    {
        if (!Util.IsSupportedConsole())
        {
            Util.StartInSupportedConsole(Util.IsRunAsAdmin());
            return;
        }

        PInvoke.SetProcessDpiAwarenessContext(PInvoke.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        using (var provider = new ServiceProvider())
        {
            provider.GetService<ILogger>().LogInfo($"{nameof(ClipTypr)} has started{(Util.IsRunAsAdmin() ? " in Admin Mode" : "")}");

            using (var guard = provider.GetService<StartupGuard>())
            {
                if (!guard.WaitForAccess())
                {
                    provider.GetService<ILogger>().LogCritical($"{nameof(ClipTypr)} is already running", null);
                    Environment.FailFast($"{nameof(ClipTypr)} is already running");
                }

                provider.GetService<ServiceRunner>().RunAsync().GetAwaiter().GetResult();
            }
        }
    }
}