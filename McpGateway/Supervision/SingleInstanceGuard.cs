using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

/// <summary>
/// One gateway per live-backend registry, enforced with a named mutex held for the process
/// lifetime.
/// <para>
/// Startup reconciliation cannot tell an orphan from a live backend of a gateway that is still
/// running: both are records naming a live process whose start time matches, and the identity check
/// passes precisely because it *is* the recorded process. So a second gateway -- started by hand, or
/// by Task Scheduler's RestartCount racing an exit that has not finished -- would kill the first
/// one's backends and then die on the port conflict, having destroyed exactly what it was meant to
/// protect. The guard is taken before the registry is touched at all.
/// </para>
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// Names held by this process. A Mutex is reentrant per thread, so without this a second
    /// Build() on the thread that took the first would silently succeed -- and then reconcile,
    /// killing the first instance's backends. Cross-process is what the named mutex is for; this
    /// closes the same-process case with the same answer.
    /// </summary>
    private static readonly HashSet<string> HeldInThisProcess = new(StringComparer.Ordinal);

    private readonly string _name;
    private Mutex? _mutex;

    private SingleInstanceGuard(string name, Mutex? mutex)
    {
        _name = name;
        _mutex = mutex;
    }

    /// <summary>
    /// Derives the mutex name from the registry path, because the registry is what it protects:
    /// two gateways sharing one are the dangerous case, and two pointed at different ones cannot
    /// hurt each other. Hashed because a mutex name cannot contain path separators.
    /// </summary>
    public static string NameFor(string liveRegistryPath) =>
        // Local\ rather than Global\: creating a Global\ object needs SeCreateGlobalPrivilege,
        // which the gateway -- registered at logon with RunLevel Limited -- does not have.
        @"Local\McpGateway-" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                Path.TrimEndingDirectorySeparator(liveRegistryPath).ToLowerInvariant())))[..32];

    /// <summary>
    /// Throws if another gateway already holds the name. The caller must do this before reading or
    /// reconciling the registry.
    /// </summary>
    public static SingleInstanceGuard Acquire(string name, ILogger logger)
    {
        lock (HeldInThisProcess)
        {
            if (!HeldInThisProcess.Add(name)) throw new InvalidOperationException(Refusal(name));
        }

        try
        {
            Mutex mutex;
            try
            {
                mutex = new Mutex(initiallyOwned: false, name);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // The name exists but we cannot open it with the rights we need -- another account
                // holds it. Same conclusion: not ours to take.
                throw new InvalidOperationException(Refusal(name), ex);
            }

            bool acquired;
            try
            {
                acquired = mutex.WaitOne(TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                // The previous gateway died without releasing it. We hold it now, and the backends
                // it left behind are exactly what reconciliation exists for. A Mutex is used rather
                // than a named Semaphore precisely for this: a semaphore's count is not restored
                // when its holder dies, so one crashed gateway would block every later one forever.
                logger.LogWarning(
                    "The previous gateway exited without releasing {Name}; its backends will be " +
                    "reconciled", name);

                acquired = true;
            }

            if (acquired) return new SingleInstanceGuard(name, mutex);

            mutex.Dispose();
            throw new InvalidOperationException(Refusal(name));
        }
        catch
        {
            lock (HeldInThisProcess) HeldInThisProcess.Remove(name);
            throw;
        }
    }

    private static string Refusal(string name) =>
        $"Another gateway is already running (it holds '{name}'). Starting a second one would " +
        "reconcile the first one's live backends as orphans and kill them, and would then fail on " +
        "the port anyway. Stop the running gateway first.";

    public void Dispose()
    {
        if (_mutex is null) return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Mutex ownership is per-thread and DI teardown rarely runs on the thread that
            // acquired it. Closing the handle below still frees the mutex -- the next gateway
            // acquires it via AbandonedMutexException, which it handles.
        }

        _mutex.Dispose();
        _mutex = null;

        lock (HeldInThisProcess) HeldInThisProcess.Remove(_name);
    }
}
