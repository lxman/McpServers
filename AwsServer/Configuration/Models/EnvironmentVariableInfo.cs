namespace AwsServer.Configuration.Models;

/// <summary>
/// Environment variable information
/// </summary>
public class EnvironmentVariableInfo
{
    public string? AwsAccessKeyId { get; set; }
    public string? AwsSecretAccessKey { get; set; }
    public string? AwsRegion { get; set; }
    public string? AwsProfile { get; set; }
    public string? AwsRoleArn { get; set; }
    public string? AwsSessionToken { get; set; }
}