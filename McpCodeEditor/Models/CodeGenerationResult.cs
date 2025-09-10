namespace McpCodeEditor.Models;

public class CodeGenerationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? GeneratedCode { get; set; }
    public string? ModifiedFileContent { get; set; }
    public string? FilePath { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
