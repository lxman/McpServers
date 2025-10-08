namespace CSharpAnalyzerMcp.Models.Reflection;

public class ListTypesResponse
{
    public bool Success { get; set; }
    public List<TypeSummary> Types { get; set; } = [];
    public int TotalCount { get; set; }
    public string? Error { get; set; }
}
