namespace AzureServer.Core.Services.Monitor.Models;

public class ApplicationInsightsDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string ApplicationType { get; set; } = string.Empty;
    public string? ApplicationId { get; set; }
    public string? InstrumentationKey { get; set; }
    public string? ConnectionString { get; set; }
    public string? ProvisioningState { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}