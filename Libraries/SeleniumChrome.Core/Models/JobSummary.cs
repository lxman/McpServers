namespace SeleniumChrome.Core.Models;

/// <summary>
/// Summary for job listing
/// </summary>
public class JobSummary
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SearchTerm { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int JobsProcessed { get; set; }
}