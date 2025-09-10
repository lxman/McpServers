namespace McpCodeEditor.Models.Options;

public class ExtractMethodOptions
{
    public string NewMethodName { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public bool IsStatic { get; set; } = false;
    public string AccessModifier { get; set; } = "private";
    public string? ReturnType { get; set; }
}
