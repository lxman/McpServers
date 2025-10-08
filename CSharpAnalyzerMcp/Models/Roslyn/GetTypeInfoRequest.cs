namespace CSharpAnalyzerMcp.Models.Roslyn;

public class GetTypeInfoRequest
{
    public string Code { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
    public string? FilePath { get; set; }
}
