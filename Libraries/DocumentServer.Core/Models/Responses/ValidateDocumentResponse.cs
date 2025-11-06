namespace DocumentServer.Core.Models.Responses;

/// <summary>
/// Response from document validation
/// </summary>
public class ValidateDocumentResponse
{
    /// <summary>
    /// Whether the document is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Path to the validated document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// List of validation errors found
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// List of validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Whether the document can be opened
    /// </summary>
    public bool CanOpen { get; set; }

    /// <summary>
    /// Whether the document appears corrupted
    /// </summary>
    public bool IsCorrupted { get; set; }

    /// <summary>
    /// Additional validation details
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}
