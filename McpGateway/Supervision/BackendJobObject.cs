using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace McpGateway.Supervision;

/// <summary>
/// A Windows job object configured with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE. Every backend the
/// gateway spawns is assigned to it, so when the gateway process terminates for any reason -- crash,
/// End Task, OOM -- the OS closes the last handle and kills the backends with it.
/// <para>
/// This is the primary defence against orphaned backends. Windows does not kill children when a
/// parent dies, and build/register-gateway-task.ps1 sets -RestartCount 3, so without it a crashed
/// gateway comes back up beside its own orphans: two code-assist processes writing the same
/// machine-wide %LocalAppData%\CodeAssist\indexes.
/// </para>
/// </summary>
public sealed class BackendJobObject : IDisposable
{
    private nint _handle;

    private BackendJobObject(nint handle) => _handle = handle;

    /// <summary>False when the job could not be created; callers fall back to the registry.</summary>
    public bool IsAvailable => _handle != nint.Zero;

    /// <summary>
    /// Failure returns an unavailable job rather than throwing: an unsupervised backend is bad, a
    /// gateway that will not start is worse. It is logged at critical because the invariant it
    /// protects -- one live instance of a non-overlap server -- then rests entirely on
    /// <see cref="LiveBackendRegistry"/>'s startup reconciliation.
    /// </summary>
    public static BackendJobObject Create(ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
        {
            logger.LogCritical(
                "Not running on Windows, so no job object guards the backends against a " +
                "non-graceful gateway exit");

            return new BackendJobObject(nint.Zero);
        }

        nint handle = Interop.CreateJobObjectW(nint.Zero, null);
        if (handle == nint.Zero)
        {
            logger.LogCritical(
                "CreateJobObject failed (win32 error {Error}); backends will survive a " +
                "non-graceful gateway exit as orphans", Marshal.GetLastWin32Error());

            return new BackendJobObject(nint.Zero);
        }

        Interop.JobObjectExtendedLimitInformation limits = default;
        limits.BasicLimitInformation.LimitFlags = Interop.JobObjectLimitKillOnJobClose;

        if (TrySetLimits(handle, limits)) return new BackendJobObject(handle);

        logger.LogCritical(
            "SetInformationJobObject(KILL_ON_JOB_CLOSE) failed (win32 error {Error}); backends " +
            "will survive a non-graceful gateway exit as orphans", Marshal.GetLastWin32Error());

        Interop.CloseHandle(handle);
        return new BackendJobObject(nint.Zero);
    }

    /// <summary>Adopts a freshly started process. False means it will outlive a gateway crash.</summary>
    public bool TryAssign(Process process)
    {
        if (!IsAvailable) return false;

        return Interop.AssignProcessToJobObject(_handle, process.Handle);
    }

    /// <summary>Whether this specific job contains the process. Verification only.</summary>
    internal bool Contains(Process process)
    {
        if (!IsAvailable) return false;

        return Interop.IsProcessInJob(process.Handle, _handle, out bool inJob) && inJob;
    }

    /// <summary>
    /// Reads the limit flags back out of the OS. A P/Invoke struct whose layout is subtly wrong can
    /// still make SetInformationJobObject return success while setting nothing useful, so the only
    /// honest check is to ask Windows what it actually stored.
    /// </summary>
    internal bool KillsOnClose
    {
        get
        {
            if (!IsAvailable) return false;

            int size = Marshal.SizeOf<Interop.JobObjectExtendedLimitInformation>();
            nint buffer = Marshal.AllocHGlobal(size);

            try
            {
                if (!Interop.QueryInformationJobObject(
                        _handle, Interop.JobObjectExtendedLimitInformationClass,
                        buffer, (uint)size, nint.Zero))
                {
                    return false;
                }

                var stored = Marshal.PtrToStructure<Interop.JobObjectExtendedLimitInformation>(buffer);

                return (stored.BasicLimitInformation.LimitFlags &
                        Interop.JobObjectLimitKillOnJobClose) != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    /// <summary>
    /// Closing the handle is what kills the members, so the gateway deliberately never disposes
    /// this: the only correct moment is process exit, and the OS does that for us. It is here so
    /// the test that proves kill-on-close actually works can trigger the same thing a crash would,
    /// without having to kill the test host.
    /// </summary>
    public void Dispose()
    {
        if (_handle == nint.Zero) return;

        Interop.CloseHandle(_handle);
        _handle = nint.Zero;
    }

    private static bool TrySetLimits(nint handle, Interop.JobObjectExtendedLimitInformation limits)
    {
        int size = Marshal.SizeOf<Interop.JobObjectExtendedLimitInformation>();
        nint buffer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(limits, buffer, fDeleteOld: false);

            return Interop.SetInformationJobObject(
                handle, Interop.JobObjectExtendedLimitInformationClass, buffer, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static class Interop
    {
        internal const uint JobObjectLimitKillOnJobClose = 0x2000;
        internal const int JobObjectExtendedLimitInformationClass = 9;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint CreateJobObjectW(nint attributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetInformationJobObject(
            nint job, int infoClass, nint info, uint infoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool QueryInformationJobObject(
            nint job, int infoClass, nint info, uint infoLength, nint returnedLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AssignProcessToJobObject(nint job, nint process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsProcessInJob(
            nint process, nint job, [MarshalAs(UnmanagedType.Bool)] out bool result);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(nint handle);

        [StructLayout(LayoutKind.Sequential)]
        internal struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public nuint MinimumWorkingSetSize;
            public nuint MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public nuint ProcessMemoryLimit;
            public nuint JobMemoryLimit;
            public nuint PeakProcessMemoryUsed;
            public nuint PeakJobMemoryUsed;
        }
    }
}
