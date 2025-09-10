namespace McpCodeEditor.Models.Refactoring;

/// <summary>
/// Common context information for refactoring operations
/// </summary>
public class RefactoringContext
{
    /// <summary>
    /// Gets or sets the file path being refactored
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the language type of the file
    /// </summary>
    public LanguageType Language { get; set; }
    
    /// <summary>
    /// Gets or sets the workspace root path
    /// </summary>
    public string WorkspaceRoot { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the file content
    /// </summary>
    public string FileContent { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets whether to create a backup before the operation
    /// </summary>
    public bool CreateBackup { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to track changes from the operation
    /// </summary>
    public bool TrackChanges { get; set; } = true;
    
    /// <summary>
    /// Gets or sets additional context data for the operation
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the operation identifier for tracking purposes
    /// </summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Gets or sets the timestamp when the operation was initiated
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
