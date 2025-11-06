namespace DebugServer.Core.Models;

/// <summary>
/// Helper class for thread information.
/// </summary>
public class ThreadInfo
{
    public string Id { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? Name { get; set; }
    public FrameInfo? Frame { get; set; }
}