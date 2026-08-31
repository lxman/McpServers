using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

/// <summary>One backend the gateway believes it started and has not yet stopped.</summary>
public sealed record LiveBackendRecord(
    string Server,
    string PoolKey,
    string Version,
    int Pid,
    int Port,
    DateTimeOffset ProcessStartedAt);

/// <summary>
/// The on-disk backstop to the job object. Windows does not kill a process when its parent dies, so
/// a gateway that is killed rather than shut down leaves every backend running -- undiscoverable
/// (the port file is deleted after a successful start), never idle-reaped, holding its deploy
/// directory against /admin/prune, and, once Task Scheduler restarts the gateway, sharing
/// machine-wide state with a freshly started instance of the same server.
/// <para>
/// The job object is the primary defence. This exists for the cases it cannot cover: orphans left
/// by a run from before the job object existed, and by a job object that failed to be created.
/// </para>
/// </summary>
public sealed class LiveBackendRegistry(string directory, ILogger logger)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();

    public string Directory { get; } = directory;

    /// <summary>
    /// Writes (or overwrites) the record for one backend. Called before the health gate rather than
    /// after: code-assist's startup timeout is 120 seconds and a gateway killed inside that window
    /// would otherwise leave behind a process nothing has ever recorded.
    /// </summary>
    public void Record(LiveBackendRecord record)
    {
        lock (_gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);

                string path = PathFor(record.Pid);
                string temp = path + ".tmp";

                File.WriteAllText(temp, JsonSerializer.Serialize(record, Options));
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Losing a record costs us an orphan we cannot reconcile later. It must not cost us
                // the backend itself, which is already running by this point.
                logger.LogError(ex,
                    "Could not record live backend {Server} (pid {Pid}); a non-graceful exit will " +
                    "leave it unreconcilable", record.Server, record.Pid);
            }
        }
    }

    /// <summary>Drops a backend's record. Called once its process is known to be gone.</summary>
    public void Forget(int pid)
    {
        lock (_gate)
        {
            try
            {
                File.Delete(PathFor(pid));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not clear the live-backend record for pid {Pid}", pid);
            }
        }
    }

    public IReadOnlyList<LiveBackendRecord> Read()
    {
        lock (_gate)
        {
            if (!System.IO.Directory.Exists(Directory)) return [];

            var records = new List<LiveBackendRecord>();

            foreach (string path in System.IO.Directory.GetFiles(Directory, "*.json"))
            {
                try
                {
                    LiveBackendRecord? record =
                        JsonSerializer.Deserialize<LiveBackendRecord>(File.ReadAllText(path), Options);

                    if (record is not null) records.Add(record);
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    logger.LogWarning(ex, "Ignoring unreadable live-backend record at {Path}", path);
                }
            }

            return records;
        }
    }

    /// <summary>
    /// Kills every recorded process that is still alive, then clears the registry. Returns how many
    /// were killed. Runs at gateway startup, before anything is served, so a restart never overlaps
    /// the backends the previous run left behind.
    /// </summary>
    public int Reconcile()
    {
        var killed = 0;

        foreach (LiveBackendRecord record in Read())
        {
            if (KillIfStillTheSameProcess(record)) killed++;

            // Cleared either way. A record that named a process which has since died -- or whose
            // pid now belongs to something else -- is stale, and keeping it only risks a later run
            // matching it against yet another innocent process.
            Forget(record.Pid);
        }

        if (killed > 0)
        {
            logger.LogWarning(
                "Killed {Count} backend(s) orphaned by a previous gateway run", killed);
        }

        return killed;
    }

    private bool KillIfStillTheSameProcess(LiveBackendRecord record)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(record.Pid);
        }
        catch (ArgumentException)
        {
            // No process with that pid: it exited without us clearing the record, which is the
            // ordinary case after a gateway crash that the backends did not survive.
            return false;
        }

        using (process)
        {
            try
            {
                // Windows reuses pids aggressively. Without this the registry is a licence to kill
                // whatever unrelated process happens to hold the number now.
                DateTimeOffset actual = new(process.StartTime);
                if ((actual - record.ProcessStartedAt).Duration() > IdentityTolerance)
                {
                    logger.LogInformation(
                        "Pid {Pid} is alive but started at {Actual}, not {Recorded}; it is not the " +
                        "{Server} backend we recorded, so it is left alone",
                        record.Pid, actual, record.ProcessStartedAt, record.Server);

                    return false;
                }

                logger.LogWarning(
                    "Killing orphaned {Server} backend (pid {Pid}, version {Version}) left by a " +
                    "previous gateway run", record.Server, record.Pid, record.Version);

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);

                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or SystemException)
            {
                // Exited between GetProcessById and here, or we cannot read/kill it. Either way we
                // must not claim a kill we did not make.
                logger.LogWarning(ex,
                    "Could not reconcile pid {Pid} recorded for {Server}", record.Pid, record.Server);

                return false;
            }
        }
    }

    /// <summary>
    /// Process.StartTime round-trips through JSON at full tick precision, so an exact match would
    /// normally hold. A second of slack costs nothing -- a recycled pid landing within a second of
    /// the original's start time is not a case worth optimising for -- and survives any future
    /// change to how the timestamp is stored.
    /// </summary>
    private static readonly TimeSpan IdentityTolerance = TimeSpan.FromSeconds(1);

    private string PathFor(int pid) => Path.Combine(Directory, $"{pid}.json");
}
