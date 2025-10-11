namespace AzureServer.Services.AppService.Models;

public class WebAppDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? DefaultHostName { get; set; }
    public List<string>? HostNames { get; set; }
    public string? RepositorySiteName { get; set; }
    public string? UsageState { get; set; }
    public bool Enabled { get; set; }
    public List<string>? EnabledHostNames { get; set; }
    public string? AvailabilityState { get; set; }
    public DateTime? LastModifiedTimeUtc { get; set; }
    public string? OutboundIpAddresses { get; set; }
    public string? PossibleOutboundIpAddresses { get; set; }
    public string? AppServicePlanId { get; set; }
    public string? ServerFarmId { get; set; }
    public bool? HttpsOnly { get; set; }
    public bool? ClientAffinityEnabled { get; set; }
    public bool? ClientCertEnabled { get; set; }
    public string? ClientCertMode { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string? RuntimeStack { get; set; }
    public string? SiteUrl { get; set; }
}
