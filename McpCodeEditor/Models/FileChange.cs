namespace McpCodeEditor.Models;

public class FileChange
{
    public string FilePath { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public string ModifiedContent { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public List<LineChange> LineChanges { get; set; } = [];
}
