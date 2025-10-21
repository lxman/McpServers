namespace PlaywrightServerMcp.Models;

/// <summary>
/// Asset analysis for bundle optimization
/// </summary>
public class AssetAnalysis
{
    public string AssetName { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty; // js, css, images, fonts
    public long SizeBytes { get; set; }
    public long GzippedSizeBytes { get; set; }
    public double PercentageOfBundle { get; set; }
    public bool IsOptimized { get; set; }
    public AssetOptimization Optimization { get; set; } = new();
    public List<string> OptimizationSuggestions { get; set; } = [];
}