namespace CSharpAnalyzerMcp.Models.Roslyn;

public class FindDeadCodeResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<DeadCodeInfo> DeadCode { get; set; } = new();
    public int TotalCount { get; set; }
}

public class DeadCodeInfo
{
    public string? Kind { get; set; } // "UnreachableCode", "UnusedMethod", "UnusedField", etc.
    public string? Name { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string? Message { get; set; }
    public string? Suggestion { get; set; }
}
