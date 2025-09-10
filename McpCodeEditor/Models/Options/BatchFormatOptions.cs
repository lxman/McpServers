namespace McpCodeEditor.Models.Options;

/// <summary>
/// Options for bulk formatting operations
/// </summary>
public class BatchFormatOptions
{
    /// <summary>
    /// File pattern to match for formatting (e.g., "*.cs")
    /// </summary>
    public string FilePattern { get; set; } = "*.cs";

    /// <summary>
    /// Directories to exclude from formatting operation
    /// </summary>
    public List<string> ExcludeDirectories { get; set; } = [];

    /// <summary>
    /// Whether to create backup before formatting
    /// </summary>
    public bool CreateBackup { get; set; } = true;

    /// <summary>
    /// Code formatting style to apply (future: support different formatting styles)
    /// </summary>
    public string? CodeStyle { get; set; }
}
