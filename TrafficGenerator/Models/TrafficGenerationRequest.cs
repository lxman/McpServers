using System.ComponentModel.DataAnnotations;

namespace TrafficGenerator.Models;

public abstract class TrafficGenerationRequest
{
    [Required] public string TrafficType { get; set; } = string.Empty;
    public int DurationSeconds { get; set; } = 60;
    public string? SourceInterface { get; set; }
    public string? TargetHost { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}