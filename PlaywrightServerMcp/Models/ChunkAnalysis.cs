namespace PlaywrightServerMcp.Models;

/// <summary>
/// Chunk analysis for bundle optimization
/// </summary>
public class ChunkAnalysis
{
    public List<ChunkInfo> Chunks { get; set; } = [];
    public ChunkOptimization Optimization { get; set; } = new();
    public List<string> OptimizationOpportunities { get; set; } = [];
    public SplittingStrategy RecommendedStrategy { get; set; } = new();
}