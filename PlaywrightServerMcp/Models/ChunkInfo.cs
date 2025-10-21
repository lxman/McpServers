namespace PlaywrightServerMcp.Models;

/// <summary>
/// Individual chunk information
/// </summary>
public class ChunkInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // initial, async, runtime
    public long SizeBytes { get; set; }
    public long GzippedSizeBytes { get; set; }
    public double PercentageOfBundle { get; set; }
    public List<string> Modules { get; set; } = [];
    public List<string> Components { get; set; } = [];
    public bool IsLazyLoaded { get; set; }
    public string LoadPriority { get; set; } = string.Empty; // high, medium, low
}