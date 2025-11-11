using System.Text.Json;
using System.Text.RegularExpressions;
using Mcp.Common.Core;

namespace Mcp.Database.Core.Common;

/// <summary>
/// Contains metadata about a database connection including health status and performance metrics.
/// </summary>
public class ConnectionInfo
{
    /// <summary>
    /// Gets or sets the unique name/identifier for this connection.
    /// </summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the connection string (masked for security when serialized).
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database name for this connection.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this connection was established.
    /// </summary>
    public DateTime ConnectedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last health check (ping).
    /// </summary>
    public DateTime LastPing { get; set; }

    /// <summary>
    /// Gets or sets whether the connection is currently healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the duration of the last ping operation.
    /// </summary>
    public TimeSpan? LastPingDuration { get; set; }

    /// <summary>
    /// Gets or sets the database type (e.g., "MongoDB", "Redis", "SQL").
    /// </summary>
    public string DatabaseType { get; set; } = string.Empty;

    /// <summary>
    /// Updates the health status and ping information for this connection.
    /// </summary>
    /// <param name="isHealthy">Whether the connection is currently healthy</param>
    /// <param name="pingDuration">Optional duration of the ping operation</param>
    public void UpdateHealth(bool isHealthy, TimeSpan? pingDuration = null)
    {
        IsHealthy = isHealthy;
        LastPing = DateTime.UtcNow;
        LastPingDuration = pingDuration;
    }

    /// <summary>
    /// Converts the connection info to a JSON string with masked credentials.
    /// </summary>
    /// <returns>JSON representation with masked connection string</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            ConnectionName,
            ConnectionString = MaskConnectionString(ConnectionString),
            DatabaseName,
            DatabaseType,
            ConnectedAt,
            LastPing,
            IsHealthy,
            LastPingMs = LastPingDuration?.TotalMilliseconds
        }, SerializerOptions.JsonOptionsIndented);
    }

    /// <summary>
    /// Masks sensitive information in connection strings (passwords, tokens, etc.).
    /// </summary>
    /// <param name="connectionString">The connection string to mask</param>
    /// <returns>Masked connection string with credentials hidden</returns>
    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;

        try
        {
            // Try parsing as URI first (works for MongoDB, Redis, PostgreSQL, MySQL)
            var uri = new Uri(connectionString);
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                string[] userInfo = uri.UserInfo.Split(':');
                string maskedUserInfo = userInfo.Length > 1 ? $"{userInfo[0]}:***" : userInfo[0];
                return connectionString.Replace(uri.UserInfo, maskedUserInfo);
            }
        }
        catch
        {
            // If URI parsing fails, use regex patterns for common connection string formats

            // Pattern for "user:password@host" (MongoDB, Redis)
            connectionString = Regex.Replace(connectionString,
                @"(:\\/\\/[^:]+:)[^@]+(@)", "$1***$2");

            // Pattern for "Password=..." or "Pwd=..." (SQL Server, etc.)
            connectionString = Regex.Replace(connectionString,
                @"(Password|Pwd)=([^;]+)", "$1=***", RegexOptions.IgnoreCase);

            // Pattern for "Access Token=..." (Azure SQL)
            connectionString = Regex.Replace(connectionString,
                @"(Access Token)=([^;]+)", "$1=***", RegexOptions.IgnoreCase);
        }

        return connectionString;
    }
}
