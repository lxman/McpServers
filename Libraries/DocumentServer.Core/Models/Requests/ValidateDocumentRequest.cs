namespace DocumentServer.Core.Models.Requests;

/// <summary>
/// Request to validate a document
/// </summary>
public class ValidateDocumentRequest
{
    /// <summary>
    /// Full path to the document file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to perform deep validation (default: true)
    /// </summary>
    public bool DeepValidation { get; set; } = true;
}
