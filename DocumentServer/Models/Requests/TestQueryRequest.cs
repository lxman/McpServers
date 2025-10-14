namespace DocumentServer.Models.Requests;

/// <summary>
/// Request to test a query against an index without returning full results
/// </summary>
public class TestQueryRequest
{
    /// <summary>
    /// Search query string to test
    /// </summary>
    public string Query { get; set; } = string.Empty;
}
