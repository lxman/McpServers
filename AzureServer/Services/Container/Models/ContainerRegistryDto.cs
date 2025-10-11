namespace AzureServer.Services.Container.Models;

public class ContainerRegistryDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? LoginServer { get; set; }
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? SkuName { get; set; }
    public string? SkuTier { get; set; }
    public string? ProvisioningState { get; set; }
    public bool? AdminUserEnabled { get; set; }
    public DateTime? CreationDate { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public bool? PublicNetworkAccess { get; set; }
    public string? NetworkRuleSetDefaultAction { get; set; }
    public List<string>? Policies { get; set; }
}

public class ContainerImageDto
{
    public string? Registry { get; set; }
    public string? Repository { get; set; }
    public string? Tag { get; set; }
    public string? Digest { get; set; }
    public long? Size { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? Architecture { get; set; }
    public string? Os { get; set; }
}

public class ContainerRepositoryDto
{
    public string? Name { get; set; }
    public string? Registry { get; set; }
    public int? TagCount { get; set; }
    public int? ManifestCount { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class RegistryCredentialsDto
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Password2 { get; set; }
}
