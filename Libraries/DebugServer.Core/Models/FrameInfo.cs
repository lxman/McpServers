namespace DebugServer.Core.Models;

/// <summary>
/// Helper class for frame information.
/// </summary>
public class FrameInfo
{
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Function { get; set; }
}