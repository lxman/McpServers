namespace AzureServer.Services.Sql.DbManagement.Models;

/// <summary>
/// Represents an Azure SQL Elastic Pool
/// </summary>
public class ElasticPoolDto
{
    public string Name { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? Edition { get; set; }
    public int? Dtu { get; set; }
    public int? DatabaseDtuMax { get; set; }
    public int? DatabaseDtuMin { get; set; }
    public long? StorageMB { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
