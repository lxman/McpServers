namespace McpCodeEditor.Models;

public class ChangeRecord
{
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? BackupId { get; set; }
    public string Description { get; set; } = string.Empty;
    public ChangeDetails Details { get; set; } = new();
    public string UserId { get; set; } = "mcp-user";
    public bool IsUndone { get; set; } = false;
    public string? UndoChangeId { get; set; }
    public string? RedoChangeId { get; set; }
}

public class ChangeDetails
{
    public long OriginalSize { get; set; }
    public long ModifiedSize { get; set; }
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public int LinesModified { get; set; }
    public string? ContentHash { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class UndoRedoResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ChangeId { get; set; }
    public string? UndoRedoChangeId { get; set; }
    public string? FilePath { get; set; }
    public string? Operation { get; set; }
    public ChangeRecord? UndoChangeRecord { get; set; }
    
    /// <summary>
    /// Indicates if the calling service needs to track this undo/redo operation as a change
    /// </summary>
    public bool RequiresChangeTracking { get; set; } = false;
    
    /// <summary>
    /// Additional content information for the undo/redo operation
    /// </summary>
    public Dictionary<string, object>? UndoContent { get; set; }
}
