using DesktopDriver.Services.AdvancedFileEditing.Models;

namespace DesktopDriver.Services.AdvancedFileEditing.Models;

/// <summary>
/// Represents a pending edit operation that requires approval before being applied
/// </summary>
public class PendingEdit
{
    /// <summary>
    /// Unique approval token for this pending edit
    /// </summary>
    public string ApprovalToken { get; init; } = string.Empty;
    
    /// <summary>
    /// Path to the file being edited
    /// </summary>
    public string FilePath { get; init; } = string.Empty;
    
    /// <summary>
    /// The edit operation to be performed
    /// </summary>
    public EditOperation Operation { get; init; } = null!;
    
    /// <summary>
    /// Version token of the file when the edit was prepared
    /// </summary>
    public string OriginalVersionToken { get; init; } = string.Empty;
    
    /// <summary>
    /// Complete preview of the file content after the edit
    /// </summary>
    public string PreviewContent { get; init; } = string.Empty;
    
    /// <summary>
    /// Diff showing the changes
    /// </summary>
    public string DiffPreview { get; init; } = string.Empty;
    
    /// <summary>
    /// Number of lines that will be affected
    /// </summary>
    public int LinesAffected { get; init; }
    
    /// <summary>
    /// When this pending edit was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this pending edit expires
    /// </summary>
    public DateTime ExpiresAt { get; init; }
    
    /// <summary>
    /// Whether backup should be created when applied
    /// </summary>
    public bool CreateBackup { get; init; }
    
    /// <summary>
    /// Operation-specific metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}