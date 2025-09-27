namespace OfficeMcp.Models;

public class ServiceStatusInfo
{
    public bool IsHealthy { get; set; }
    public int LoadedDocumentCount { get; set; }
    public int MaxDocuments { get; set; }
    public long TotalFileSize { get; set; }
    public int WordDocuments { get; set; }
    public int ExcelDocuments { get; set; }
    public int PowerPointDocuments { get; set; }
    public MemoryUsageInfo MemoryUsage { get; set; } = new();
    public DateTime Uptime { get; set; }
}