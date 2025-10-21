namespace PlaywrightServerMcp.Models;

public class MemorySnapshot
{
    public DateTime Timestamp { get; set; }
    public long JsHeapSizeLimit { get; set; }
    public long TotalJsHeapSize { get; set; }
    public long UsedJsHeapSize { get; set; }
    public double CpuUsage { get; set; }
    public int DomNodes { get; set; }
    public int JsListeners { get; set; }
}