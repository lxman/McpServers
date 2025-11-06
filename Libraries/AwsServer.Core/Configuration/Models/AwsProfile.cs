namespace AwsServer.Core.Configuration.Models;

/// <summary>
/// AWS profile information from CLI configuration
/// </summary>
public class AwsProfile
{
    public string Name { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? Output { get; set; }
    public bool HasAccessKey { get; set; }
    public bool HasSecretKey { get; set; }
}