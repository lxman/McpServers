namespace AzureMcp.Configuration;

public class DevOpsOptions
{
    public string OrganizationUrl { get; set; } = string.Empty;
    public string CredentialTarget { get; set; } = "AzureDevOps";
    public string? DefaultProject { get; set; }
}
