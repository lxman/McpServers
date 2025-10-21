namespace PlaywrightServerMcp.Models;

/// <summary>
/// Network performance metrics
/// </summary>
public class NetworkMetrics
{
    public int RequestCount { get; set; }
    public long TotalTransferSize { get; set; }
    public long CompressedSize { get; set; }
    public double CompressionRatio { get; set; }
    public int CacheableResources { get; set; }
}