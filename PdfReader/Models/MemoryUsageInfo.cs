namespace PdfMcp.Models;

public class MemoryUsageInfo
{
    public long WorkingSet { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
}