namespace DesktopCommanderMcp.Models;

/// <summary>
/// Represents a managed service instance
/// </summary>
public class ManagedService
{
    public required string ServiceId { get; init; }
    public required string ServiceName { get; init; }
    public required int ProcessId { get; init; }
    public int? Port { get; init; }
    public DateTime StartedAt { get; init; }
}