namespace DebugMcp.Models;

public class DebugSession
{
    public required string SessionId { get; set; }
    public required string ExecutablePath { get; set; }
    public required string WorkingDirectory { get; set; }
    public string? Arguments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DebugSessionState State { get; set; }
    public List<Breakpoint> Breakpoints { get; set; } = [];
}