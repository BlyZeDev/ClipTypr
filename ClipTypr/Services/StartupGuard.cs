namespace ClipTypr.Services;

using System.ComponentModel;
using System.Diagnostics;

public sealed class StartupGuard : IDisposable
{
    private const string Guid = "192f6bfa-09b1-4b64-8304-5e5421907dd4";
    private const string MutexId = $@"Global\{{{nameof(ClipTypr)}-{Guid}}}";
    private const int MutexTimeoutMs = 5000;

    private readonly Mutex _mutex;

    private bool hasHandle;

    public StartupGuard()
    {
        _mutex = new Mutex(false, MutexId, out _);
    }

    public bool WaitForAccess()
    {
        try
        {
            hasHandle = _mutex.WaitOne(MutexTimeoutMs, false);
        }
        catch (AbandonedMutexException)
        {
            hasHandle = true;
        }

        return hasHandle;
    }

    public void Dispose()
    {
        try
        {
            if (hasHandle) _mutex.ReleaseMutex();
            hasHandle = false;
        }
        catch { }
        finally
        {
            _mutex.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    ~StartupGuard() => Dispose();
}