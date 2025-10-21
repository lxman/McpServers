namespace PlaywrightServerMcp.Models;

/// <summary>
/// Chunk optimization analysis
/// </summary>
public class ChunkOptimization
{
    public bool ProperCodeSplitting { get; set; }
    public bool OptimalChunkSizes { get; set; }
    public bool HasDuplicatedCode { get; set; }
    public int ChunkCount { get; set; }
    public double AverageChunkSize { get; set; }
    public List<string> LargeChunks { get; set; } = [];
    public List<string> SmallChunks { get; set; } = [];
}