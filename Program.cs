namespace ClipTypr;

using ClipTypr.Common;
using ClipTypr.NATIVE;

sealed class Program
{
    private const string Guid = "192f6bfa-09b1-4b64-8304-5e5421907dd4";
    private const string MutexId = $@"Global\{{{nameof(ClipTypr)}-{Guid}}}";
    private const int MutexTimeoutMs = 5000;

    static void Main(string[] args)
    {
        using (var mutex = new Mutex(false, MutexId, out _))
        {
            var hasHandle = false;

            try
            {
                try
                {
                    Console.WriteLine($"Waiting for exclusive access - {MutexTimeoutMs / 1000d} seconds");

                    hasHandle = mutex.WaitOne(MutexTimeoutMs, false);
                    if (!hasHandle)
                    {
                        var message = "No exlusive access was available, due to the program already running";

                        _ = Native.ShowHelpMessage(
                            nint.Zero,
                            "The program is already running.\nIf you are 100% sure the program is not running, click the Help button.",
                            "Already running",
                            Native.MB_ICONERROR,
                            helpInfo => Util.OpenGitHubIssue(ServiceRunner.Version, message, Environment.StackTrace));

                        throw new TimeoutException(message);
                    }

                    Console.Clear();

                    using (var runner = ServiceRunner.Initialize(args))
                    {
                        runner.RunAndBlock();
                    }
                }
                catch (AbandonedMutexException)
                {
                    hasHandle = true;
                }
            }
            finally
            {
                if (hasHandle) mutex.ReleaseMutex();
            }
        }
    }
}