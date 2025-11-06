using AwsServer.Core.Configuration;

namespace AwsServer.Core.Models.Requests;

public class InitializeQuickSightRequest
{
    public required AwsConfiguration Config { get; set; }
    public required string AwsAccountId { get; set; }
}