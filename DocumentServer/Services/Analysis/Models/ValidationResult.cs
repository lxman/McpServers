namespace DocumentServer.Services.Analysis.Models;

/// <summary>
/// Results of document validation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Full path to the validated document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Overall validation status
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Whether the document can be opened
    /// </summary>
    public bool CanOpen { get; set; }

    /// <summary>

    /// <summary>
    /// Whether the document appears to be corrupted
    /// </summary>
    public bool IsCorrupted { get; set; }

    /// Whether the document is encrypted
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Document type
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Last modification timestamp
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// Length of extracted text content
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// List of validation warnings
    /// </summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// Extracted metadata
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}