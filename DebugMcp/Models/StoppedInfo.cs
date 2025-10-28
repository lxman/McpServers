namespace DebugMcp.Models;

/// <summary>
/// Helper class for parsed stop information.
/// </summary>
public class StoppedInfo
{
    public string? Reason { get; set; }
    public string? ThreadId { get; set; }
    public int? BreakpointNumber { get; set; }
    public string? ExitCode { get; set; }
    public FrameInfo? Frame { get; set; }
}