namespace AwsServer.Configuration.Models;

/// <summary>
/// AWS CLI configuration information
/// </summary>
public class CliConfiguration
{
    public bool ConfigFileExists { get; set; }
    public bool CredentialsFileExists { get; set; }
    public string ConfigFilePath { get; set; } = string.Empty;
    public string CredentialsFilePath { get; set; } = string.Empty;
    public List<AwsProfile> Profiles { get; set; } = [];
    public string CurrentProfile { get; set; } = "default";
    public string? DetectionError { get; set; }
}