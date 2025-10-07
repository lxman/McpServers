namespace AzureMcp.Services.AppService.Models;

public class AppSettingsDto
{
    public Dictionary<string, string> Settings { get; set; } = new();
}

public class ConnectionStringDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class AppServicePlanDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string? SkuName { get; set; }
    public string? SkuTier { get; set; }
    public int? SkuCapacity { get; set; }
    public string? Status { get; set; }
    public int? NumberOfSites { get; set; }
    public int? MaximumNumberOfWorkers { get; set; }
    public bool? IsSpot { get; set; }
    public bool? HyperV { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
