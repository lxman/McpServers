namespace DebugMcp.Models;

/// <summary>
/// Helper class for stack frame information.
/// </summary>
public class StackFrameInfo
{
    public int Level { get; set; }
    public string? Function { get; set; }
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Address { get; set; }
}