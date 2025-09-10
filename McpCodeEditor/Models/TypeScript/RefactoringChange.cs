namespace McpCodeEditor.Models.TypeScript;

public class RefactoringChange
{
    public required string FilePath { get; set; }
    public required string ChangeType { get; set; }
    public required string Description { get; set; }
    public int LineNumber { get; set; }
    public required string NewText { get; set; }
    public required string OldText { get; set; }
}
