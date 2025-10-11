namespace AzureServer.Services.Sql.QueryExecution.Models;

/// <summary>
/// Represents SQL database connection information
/// </summary>
public class ConnectionInfoDto
{
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = "AzureSQL"; // AzureSQL, PostgreSQL, MySQL
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int Port { get; set; } = 1433; // Default SQL Server port
    public bool IntegratedSecurity { get; set; }
    public bool UseAzureAD { get; set; } = true;
    public int ConnectionTimeout { get; set; } = 30;
    public bool Encrypt { get; set; } = true;
    public bool TrustServerCertificate { get; set; }
    public Dictionary<string, string>? AdditionalParameters { get; set; }
}
