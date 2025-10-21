namespace PlaywrightServerMcp.Models;

/// <summary>
/// Result structure for Angular bundle size analysis
/// </summary>
public class BundleSizeAnalysisResult
{
    public bool Success { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool AngularProjectDetected { get; set; }
    public bool WebpackStatsAvailable { get; set; }
    public BundleOverview Overview { get; set; } = new();
    public List<ComponentBundleImpact> ComponentImpacts { get; set; } = [];
    public List<ModuleBundleImpact> ModuleImpacts { get; set; } = [];
    public List<AssetAnalysis> Assets { get; set; } = [];
    public ChunkAnalysis Chunks { get; set; } = new();
    public BundleSizeDependencyAnalysis BundleSizeDependencies { get; set; } = new();
    public BundleOptimizationRecommendations Recommendations { get; set; } = new();
    public PerformanceMetrics Performance { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
}