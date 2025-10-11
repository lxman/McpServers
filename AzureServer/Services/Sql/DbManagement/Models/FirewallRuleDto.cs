namespace AzureServer.Services.Sql.DbManagement.Models;

/// <summary>
/// Represents an Azure SQL Server firewall rule
/// </summary>
public class FirewallRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string StartIpAddress { get; set; } = string.Empty;
    public string EndIpAddress { get; set; } = string.Empty;
}
