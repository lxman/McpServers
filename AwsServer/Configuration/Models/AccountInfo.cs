namespace AwsServer.Configuration.Models;

/// <summary>
/// AWS account information from STS GetCallerIdentity
/// </summary>
public class AccountInfo
{
    public string AccountId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Arn { get; set; } = string.Empty;
    public string PrincipalName { get; set; } = string.Empty;
    public bool IsGovCloud { get; set; }
    public string InferredRegion { get; set; } = string.Empty;
    public string ConfiguredRegion { get; set; } = string.Empty;
}