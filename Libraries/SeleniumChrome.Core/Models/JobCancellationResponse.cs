using SeleniumChrome.Core.Services.Enhanced;

namespace SeleniumChrome.Core.Models;

/// <summary>
/// Response for cancellation request
/// </summary>
public class JobCancellationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public BulkProcessingResult? PartialResults { get; set; }
    public int JobsProcessed { get; set; }
}