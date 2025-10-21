namespace PlaywrightServerMcp.Models;

/// <summary>
/// Bundle size distribution across different categories
/// </summary>
public class BundleSizeDistribution
{
    public long VendorSize { get; set; }
    public long ApplicationSize { get; set; }
    public long RuntimeSize { get; set; }
    public long PolyfillsSize { get; set; }
    public long StylesSize { get; set; }
    public Dictionary<string, long> ByChunk { get; set; } = new();
    public Dictionary<string, double> PercentageByCategory { get; set; } = new();
}