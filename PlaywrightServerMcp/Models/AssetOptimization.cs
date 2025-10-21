namespace PlaywrightServerMcp.Models;

/// <summary>
/// Asset optimization details
/// </summary>
public class AssetOptimization
{
    public bool Minified { get; set; }
    public bool Compressed { get; set; }
    public bool TreeShaken { get; set; }
    public bool HasSourceMaps { get; set; }
    public string CompressionRatio { get; set; } = string.Empty;
    public List<string> OptimizationTechniques { get; set; } = [];
}