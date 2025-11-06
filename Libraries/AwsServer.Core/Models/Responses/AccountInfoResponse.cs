namespace AwsServer.Core.Models.Responses;

public class AccountInfoResponse
{
    public required string AccountId { get; set; }
    public required string Region { get; set; }
    public required string Arn { get; set; }
    public required string UserId { get; set; }
    public required string ProfileName { get; set; }
}