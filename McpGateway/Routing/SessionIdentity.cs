using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace McpGateway.Routing;

/// <summary>
/// Maps a loopback TCP connection back to the process that opened it.
/// <para>
/// This exists because the MCP protocol gives us nothing to work with. The Stage 0 capture showed
/// Claude Code sends no per-session value of any kind: the 2026-07-28 revision it negotiates removed
/// <c>Mcp-Session-Id</c>, and the only identity header on the wire is the static <c>X-Mcp-Client</c>
/// we put in the client config ourselves. With Claude Desktop retired there is exactly one client,
/// so a <c>per-client</c> pool degenerates to a single shared backend and the isolation stdio gave
/// every session for free is simply gone.
/// </para>
/// <para>
/// The OS still knows what the protocol forgot. One Claude Code session is one <c>claude</c>
/// process, the gateway only ever listens on loopback, and Windows will name the owner of any
/// loopback socket. That owner is the session key.
/// </para>
/// </summary>
public static class SessionIdentity
{
    /// <summary>
    /// The pid owning the loopback connection <c>127.0.0.1:clientPort -> 127.0.0.1:gatewayPort</c>,
    /// or null when no such connection exists or the table cannot be read.
    /// <para>
    /// Both ports are matched, not just the client's. A client process holds connections to many
    /// listeners, and keying off the local port alone would hand the gateway a pid for a socket
    /// that has nothing to do with it.
    /// </para>
    /// </summary>
    /// <summary>
    /// The pool key for one session. The start time is not decoration: Windows reuses pids freely,
    /// and a key of the pid alone would hand a brand new session the backend -- and the ambient
    /// state -- of a dead one that happened to hold the same number. This is the same pid+start-time
    /// identity <see cref="Supervision.LiveBackendRegistry"/> uses to tell an orphan from a live
    /// backend.
    /// </summary>
    public static string FormatKey(int pid, DateTimeOffset startedAt) =>
        $"s-{pid}-{startedAt.ToUnixTimeMilliseconds()}";

    /// <summary>
    /// The session key for a loopback connection, or null when the owner cannot be established.
    /// Null is not an error: the caller falls back to the shared key, which is exactly how the
    /// gateway behaved before per-session pooling existed.
    /// </summary>
    public static string? TryResolveKey(int clientPort, int gatewayPort)
    {
        int? pid = TryResolvePid(clientPort, gatewayPort);
        if (pid is null) return null;

        try
        {
            using Process process = Process.GetProcessById(pid.Value);

            return FormatKey(pid.Value, process.StartTime);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                       or System.ComponentModel.Win32Exception)
        {
            // The process died between reading the table and asking about it, or it is not ours to
            // inspect. Either way there is no honest session key to hand back.
            return null;
        }
    }

    public static int? TryResolvePid(int clientPort, int gatewayPort)
    {
        if (!OperatingSystem.IsWindows()) return null;

        nint buffer = nint.Zero;
        int size = 0;

        try
        {
            // Sized in a loop rather than once: the table can grow between the sizing call and the
            // reading call, and the OS answers that with INSUFFICIENT_BUFFER again rather than a
            // partial table. Three attempts is plenty for a table that changes this slowly.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint status = Interop.GetExtendedTcpTable(
                    buffer, ref size, order: false,
                    Interop.AfInet, Interop.TcpTableOwnerPidConnections, reserved: 0);

                if (status == Interop.NoError) return ScanTable(buffer, clientPort, gatewayPort);
                if (status != Interop.ErrorInsufficientBuffer) return null;

                if (buffer != nint.Zero) Marshal.FreeHGlobal(buffer);
                buffer = Marshal.AllocHGlobal(size);
            }

            return null;
        }
        catch (OutOfMemoryException)
        {
            // A pool key is not worth taking the gateway down for. The caller falls back to the
            // shared "default" key, which is exactly the behaviour we had before this existed.
            return null;
        }
        finally
        {
            if (buffer != nint.Zero) Marshal.FreeHGlobal(buffer);
        }
    }

    private static int? ScanTable(nint buffer, int clientPort, int gatewayPort)
    {
        if (buffer == nint.Zero) return null;

        int rows = Marshal.ReadInt32(buffer);
        int rowSize = Marshal.SizeOf<Interop.TcpRowOwnerPid>();

        for (int i = 0; i < rows; i++)
        {
            var row = Marshal.PtrToStructure<Interop.TcpRowOwnerPid>(
                buffer + sizeof(int) + (i * rowSize));

            // The gateway binds loopback only, so both ends of a connection it serves are
            // 127.0.0.1. Checking that as well as the ports keeps an identical port pair on a real
            // interface from being mistaken for one of ours.
            if (row.LocalAddress != Interop.LoopbackAddress) continue;
            if (row.RemoteAddress != Interop.LoopbackAddress) continue;

            if (Port(row.LocalPort) != clientPort) continue;
            if (Port(row.RemotePort) != gatewayPort) continue;

            return row.OwningPid;
        }

        return null;
    }

    /// <summary>
    /// Win32 stores the port in the low word, in network byte order, and leaves the high word
    /// undefined. Reading the DWORD as a port is the classic way to get this wrong: it compiles,
    /// runs, and yields a number that is never a real port.
    /// </summary>
    private static int Port(uint raw) => IPAddress.NetworkToHostOrder((short)(raw & 0xFFFF)) & 0xFFFF;

    private static class Interop
    {
        internal const uint NoError = 0;
        internal const uint ErrorInsufficientBuffer = 122;
        internal const uint AfInet = 2;
        internal const int TcpTableOwnerPidConnections = 4;

        /// <summary>127.0.0.1 as stored in the table: network byte order, little-endian host.</summary>
        internal const uint LoopbackAddress = 0x0100007F;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        internal static extern uint GetExtendedTcpTable(
            nint tcpTable,
            ref int size,
            [MarshalAs(UnmanagedType.Bool)] bool order,
            uint af,
            int tableClass,
            uint reserved);

        /// <summary>MIB_TCPROW_OWNER_PID. Six DWORDs, no padding.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct TcpRowOwnerPid
        {
            public uint State;
            public uint LocalAddress;
            public uint LocalPort;
            public uint RemoteAddress;
            public uint RemotePort;
            public int OwningPid;
        }
    }
}
