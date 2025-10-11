namespace CSharpAnalyzer.Models.Roslyn;

public class RemoveUnusedUsingsResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? CleanedCode { get; set; }
    public List<string> RemovedUsings { get; set; } = new();
    public int RemovedCount { get; set; }
}
