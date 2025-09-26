using System.Text.Json;

namespace MongoIntegration.Configuration;

public class ConnectionInfo
{
    public string ServerName { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastPing { get; set; }
    public bool IsHealthy { get; set; }
    public TimeSpan? LastPingDuration { get; set; }

    public void UpdateHealth(bool isHealthy, TimeSpan? pingDuration = null)
    {
        IsHealthy = isHealthy;
        LastPing = DateTime.UtcNow;
        LastPingDuration = pingDuration;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(new
        {
            ServerName,
            ConnectionString = MaskConnectionString(ConnectionString),
            DatabaseName,
            ConnectedAt,
            LastPing,
            IsHealthy,
            LastPingDuration = LastPingDuration?.TotalMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;

        // Mask sensitive information like passwords
        try
        {
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
            // If URI parsing fails, just mask potential password patterns
            return System.Text.RegularExpressions.Regex.Replace(connectionString, 
                @"(:\/\/[^:]+:)[^@]+(@)", "$1***$2");
        }

        return connectionString;
    }
}
