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

        Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        using (var provider = new ServiceProvider())
        {
            using (var guard = provider.GetService<StartupGuard>())
            {
                var hasAccess = guard.WaitForAccess();

                if (!hasAccess) Environment.FailFast($"{nameof(ClipTypr)} is already running");

                provider.GetService<ServiceRunner>().RunAsync().GetAwaiter().GetResult();
            }
        }
    }
}