namespace AzureMcp.Services.Sql.DbManagement.Models;

/// <summary>
/// Represents an Azure SQL Database
/// </summary>
public class DatabaseDto
{
    public string Name { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CollationName { get; set; }
    public DateTime? CreationDate { get; set; }
    public string? DatabaseId { get; set; }
    public string? Edition { get; set; }
    public string? ServiceObjectiveName { get; set; }
    public long? MaxSizeBytes { get; set; }
    public string? Sku { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string? ElasticPoolName { get; set; }
    public string DatabaseType { get; set; } = "AzureSQL";
}
