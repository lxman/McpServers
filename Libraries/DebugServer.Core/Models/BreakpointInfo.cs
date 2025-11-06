namespace DebugServer.Core.Models;

/// <summary>
/// Helper class for parsed breakpoint information.
/// </summary>
public class BreakpointInfo
{
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public bool Verified { get; set; }
    public string? Warning { get; set; }
}