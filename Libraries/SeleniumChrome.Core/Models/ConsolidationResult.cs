namespace SeleniumChrome.Core.Models;

/// <summary>
/// Result of consolidating temporary job listings to final collection
/// </summary>
public class ConsolidationResult
{
    public bool Success { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int JobsConsolidated { get; set; }
    public int JobsSaved { get; set; }
    public string Message { get; set; } = string.Empty;
}
