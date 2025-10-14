namespace DocumentServer.Models.Responses;

/// <summary>
/// Response from deleting an index
/// </summary>
public class DeleteIndexResponse
{
    /// <summary>
    /// Whether the delete operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Message describing the result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The name of the deleted index
    /// </summary>
    public string IndexName { get; set; } = string.Empty;
}
