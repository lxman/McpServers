namespace AzureMcp.Authentication.models;

public class AzureCliInfo
{
    public bool IsLoggedIn { get; set; }
    public string? CurrentSubscription { get; set; }
    public string? TenantId { get; set; }
    public string? UserName { get; set; }
}