namespace ClipTypr;

sealed class Program
{
    static void Main()
    {
        using (var provider = new ServiceProvider())
        {
            using (var guard = provider.GetService<StartupGuard>())
            {
                var hasAccess = guard.WaitForAccess();

                if (!hasAccess) Environment.FailFast("The application is already running");

                provider.GetService<ServiceRunner>().RunAsync().GetAwaiter().GetResult();
            }
        }
    }
}