using SeleniumChrome.Core.Services.Enhanced;

namespace SeleniumChrome.Core.Models;

/// <summary>
/// Internal state tracking for a job
/// </summary>
internal class JobState
{
    public string JobId { get; set; } = string.Empty;
    public BulkProcessingRequest Request { get; set; } = new();
    public JobStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string ProgressMessage { get; set; } = string.Empty;
    public int CurrentBatch { get; set; }
    public int TotalBatches { get; set; }
    public BulkProcessingResult? Result { get; set; }
    public BulkProcessingSummary? Summary { get; set; }
    public string? ErrorMessage { get; set; }
}