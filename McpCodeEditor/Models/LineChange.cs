namespace McpCodeEditor.Models;

public class LineChange
{
    public int LineNumber { get; set; }
    public string Original { get; set; } = string.Empty;
    public string Modified { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
}
