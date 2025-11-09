using SeleniumChrome.Core.Services.Enhanced;

namespace SeleniumChrome.Core.Models;

/// <summary>
/// Response for status check
/// </summary>
public class JobStatusResponse
{
    public bool Found { get; set; }
    public string? JobId { get; set; }
    public string Status { get; set; } = "unknown";
    public string? ProgressMessage { get; set; }
    public string? SearchTerm { get; set; }
    public string? Location { get; set; }
    public int JobsProcessed { get; set; }
    public int CurrentBatch { get; set; }
    public int TotalBatches { get; set; }
    public int ElapsedSeconds { get; set; }
    public bool IsComplete { get; set; }
    public BulkProcessingSummary? Summary { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Message { get; set; }
}