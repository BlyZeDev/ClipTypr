namespace ClipTypr;

sealed class Program
{
    static async Task Main()
    {
        using (var provider = new ServiceProvider())
        {
            using (var guard = provider.GetService<StartupGuard>())
            {
                var hasAccess = guard.WaitForAccess();

                if (!hasAccess) Environment.FailFast("The application is already running");

                await provider.GetService<ServiceRunner>().RunAsync();
            }
        }
    }
}