namespace OfficeMcp.Models;

public class MemoryUsageInfo
{
    public double TotalMemoryMb { get; set; }
    public int DocumentCacheSize { get; set; }
    public long WorkingSet { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
}