namespace AwsMcp.Configuration.Models;

/// <summary>
/// Result of automatic configuration discovery
/// </summary>
public class AutoDiscoveryResult
{
    public AccountInfo? AccountInfo { get; set; }
    public CliConfiguration CliConfiguration { get; set; } = new();
    public EnvironmentVariableInfo EnvironmentVariables { get; set; } = new();
    public List<ServicePermissionTest>? ServicePermissions { get; set; }
    public RecommendedConfiguration? RecommendedConfiguration { get; set; }
    public string AuthenticationStatus { get; set; } = "Unknown";
    public string? AuthenticationError { get; set; }
    public List<string> TroubleshootingSuggestions { get; set; } = [];
}
