namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component output event definition
/// </summary>
public class ComponentOutput
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> ExpectedPayloadProperties { get; set; } = [];
    public string TriggerConditions { get; set; } = string.Empty;
}