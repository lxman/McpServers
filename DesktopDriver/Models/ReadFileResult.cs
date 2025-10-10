namespace DesktopDriver.Models;

/// <summary>
/// Paginated response for file reading operations
/// </summary>
public class ReadFileResult
{
    /// <summary>
    /// File content (lines in this page)
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of lines in the file
    /// </summary>
    public int TotalLines { get; set; }
    
    /// <summary>
    /// Number of lines returned in this response
    /// </summary>
    public int LinesReturned { get; set; }
    
    /// <summary>
    /// Starting line number (1-based)
    /// </summary>
    public int StartLine { get; set; }
    
    /// <summary>
    /// Ending line number (1-based, inclusive)
    /// </summary>
    public int EndLine { get; set; }
    
    /// <summary>
    /// Whether the file was truncated (more lines available)
    /// </summary>
    public bool IsTruncated { get; set; }
    
    /// <summary>
    /// Next line number to read (for continuation)
    /// </summary>
    public int? NextStartLine { get; set; }
    
    /// <summary>
    /// Version token for safe editing
    /// </summary>
    public string VersionToken { get; set; } = string.Empty;
    
    /// <summary>
    /// File path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// Human-readable message about the result
    /// </summary>
    public string Message { get; set; } = string.Empty;
}