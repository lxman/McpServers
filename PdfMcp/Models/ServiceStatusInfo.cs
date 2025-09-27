namespace PdfMcp.Models;

public class ServiceStatusInfo
{
    public int LoadedDocuments { get; set; }
    public long TotalFileSize { get; set; }
    public int TotalPages { get; set; }
    public MemoryUsageInfo MemoryUsage { get; set; } = new();
    public DateTime Uptime { get; set; }
}