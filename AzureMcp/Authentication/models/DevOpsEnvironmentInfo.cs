namespace AzureMcp.Authentication.models;

public class DevOpsEnvironmentInfo
{
    public string OrganizationUrl { get; set; } = string.Empty;
    public string? PersonalAccessToken { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool HasCredentials { get; set; }
    public string? CredentialTarget { get; set; }
}