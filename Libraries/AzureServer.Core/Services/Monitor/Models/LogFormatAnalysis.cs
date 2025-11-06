namespace AzureServer.Core.Services.Monitor.Models;

/// <summary>
/// Analysis of log message formats
/// </summary>
public class LogFormatAnalysis
{
    /// <summary>
    /// Detected format types and their counts
    /// </summary>
    public Dictionary<string, int> FormatCounts { get; set; } = new();
    
    /// <summary>
    /// Dominant format in the log set
    /// </summary>
    public string? DominantFormat { get; set; }
    
    /// <summary>
    /// Percentage of messages in dominant format
    /// </summary>
    public double DominantFormatPercentage { get; set; }
    
    /// <summary>
    /// Sample structured data from JSON logs (if any)
    /// </summary>
    public List<string>? JsonSampleKeys { get; set; }
    
    /// <summary>
    /// Common key-value patterns detected
    /// </summary>
    public List<string>? KeyValuePatterns { get; set; }
}