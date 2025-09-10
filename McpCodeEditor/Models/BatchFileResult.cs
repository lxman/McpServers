namespace McpCodeEditor.Models;

public class BatchFileResult
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
}
