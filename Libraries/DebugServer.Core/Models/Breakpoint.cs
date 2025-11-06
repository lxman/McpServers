namespace DebugServer.Core.Models;

public class Breakpoint
{
    public int Id { get; set; }
    public required string FilePath { get; set; }
    public int LineNumber { get; set; }
    public bool Verified { get; set; }
    public int HitCount { get; set; }
}