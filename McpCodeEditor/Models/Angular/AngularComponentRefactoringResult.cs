namespace McpCodeEditor.Models.Angular;

/// <summary>
/// Angular component refactoring result
/// </summary>
public class AngularComponentRefactoringResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ModifiedFiles { get; set; } = [];
    public List<string> CreatedFiles { get; set; } = [];
    public string? BackupId { get; set; }
    public List<string> Changes { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}
