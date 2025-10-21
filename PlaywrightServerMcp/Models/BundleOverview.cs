namespace PlaywrightServerMcp.Models;

/// <summary>
/// Overall bundle overview and statistics
/// </summary>
public class BundleOverview
{
    public long TotalSize { get; set; }
    public long GzippedSize { get; set; }
    public long UncompressedSize { get; set; }
    public int ChunkCount { get; set; }
    public int AssetCount { get; set; }
    public int ComponentCount { get; set; }
    public int ModuleCount { get; set; }
    public string BuildConfiguration { get; set; } = string.Empty;
    public DateTime AnalysisTimestamp { get; set; }
    public BundleSizeDistribution Distribution { get; set; } = new();
    public BundleComparison Comparison { get; set; } = new();
}