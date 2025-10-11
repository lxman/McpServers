namespace AzureServer.Services.AppService.Models;

public class DeploymentSlotDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SlotName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? DefaultHostName { get; set; }
    public bool Enabled { get; set; }
    public string? AvailabilityState { get; set; }
    public DateTime? LastModifiedTimeUtc { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
