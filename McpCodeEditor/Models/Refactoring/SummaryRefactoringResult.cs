namespace McpCodeEditor.Models.Refactoring;

/// <summary>
/// Abbreviated refactoring result for cross-file operations to prevent conversation-breaking large responses
/// </summary>
public class SummaryRefactoringResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public List<SummaryFileChange> Changes { get; set; } = [];
    public int FilesAffected { get; set; }
    public int TotalLinesChanged { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? BackupId { get; set; }
    
    /// <summary>
    /// Indicates whether this result contains full file content (potentially large)
    /// </summary>
    public bool ContainsFullContent { get; set; } = false;
    
    /// <summary>
    /// Warning about response size or mode
    /// </summary>
    public string? Warning { get; set; }
}

/// <summary>
/// Summary of changes to a single file
/// </summary>
public class SummaryFileChange
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public int TotalChanges { get; set; }
    public List<SummaryLineChange> SampleChanges { get; set; } = [];
    public List<int> ModifiedLineNumbers { get; set; } = [];
    public string ChangeSummary { get; set; } = string.Empty;
    
    /// <summary>
    /// Full file content (only included when ContainsFullContent = true)
    /// </summary>
    public string? OriginalContent { get; set; }
    public string? ModifiedContent { get; set; }
}

/// <summary>
/// Summary of a line change
/// </summary>
public class SummaryLineChange
{
    public int LineNumber { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string ChangeDescription { get; set; } = string.Empty;
    public string? SampleBefore { get; set; }
    public string? SampleAfter { get; set; }
    
    /// <summary>
    /// Full line content (only included when ContainsFullContent = true)
    /// </summary>
    public string? FullBefore { get; set; }
    public string? FullAfter { get; set; }
}
