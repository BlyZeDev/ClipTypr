namespace ClipTypr;

using ClipTypr.NATIVE;
using System.Diagnostics;

sealed class Program
{
    private const string MutexId = $@"Global\{{{nameof(ClipTypr)}-192f6bfa-09b1-4b64-8304-5e5421907dd4}}";
    private const int MutexWaitOneMs = 5000;

    static void Main(string[] args)
    {
        using (var mutex = new Mutex(false, MutexId, out _))
        {
            var hasHandle = false;

            try
            {
                try
                {
                    Console.WriteLine($"Waiting for exclusive access - {MutexWaitOneMs / 1000d} seconds");

                    hasHandle = mutex.WaitOne(MutexWaitOneMs, false);
                    if (!hasHandle)
                    {
                        var result = Native.ShowHelpMessage(
                            nint.Zero,
                            "The program is already running.\nIf you are 100% sure the program is not running, click the Help button.",
                            "Already running",
                            Native.MB_ICONERROR,
                            helpInfo =>
                            {
                                using (var process = new Process())
                                {
                                    process.StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "https://github.com/BlyZeDev/StrokeMyKeys/issues",
                                        UseShellExecute = true
                                    };
                                    process.Start();
                                }
                            });

                        throw new TimeoutException("No exlusive access was available, due to the program already running");
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