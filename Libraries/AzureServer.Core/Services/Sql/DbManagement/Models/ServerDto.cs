namespace AzureServer.Core.Services.Sql.DbManagement.Models;

/// <summary>
/// Represents an Azure SQL Server
/// </summary>
public class ServerDto
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? FullyQualifiedDomainName { get; set; }
    public string? AdministratorLogin { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public bool PublicNetworkAccess { get; set; }
    public string? MinimalTlsVersion { get; set; }
    public string ServerType { get; set; } = "AzureSQL";
}
