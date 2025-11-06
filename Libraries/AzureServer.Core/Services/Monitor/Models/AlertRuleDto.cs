namespace AzureServer.Core.Services.Monitor.Models;

public class AlertRuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string? EvaluationFrequency { get; set; }
    public string? WindowSize { get; set; }
    public List<string> Scopes { get; set; } = [];
    public string? ConditionQuery { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}