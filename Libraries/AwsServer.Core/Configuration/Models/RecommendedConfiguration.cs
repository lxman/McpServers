namespace AwsServer.Core.Configuration.Models;

/// <summary>
/// Recommended configuration based on discovery results
/// </summary>
public class RecommendedConfiguration
{
    public string RecommendedRegion { get; set; } = "us-east-1";
    public string InitializationStrategy { get; set; } = "Full service initialization";
    public List<string> WorkingServices { get; set; } = [];
    public List<string> FailedServices { get; set; } = [];
    public List<string> Reasoning { get; set; } = [];
    public Dictionary<string, object> OptimalSettings { get; set; } = new();
}