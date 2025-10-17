using AwsServer.Configuration;

namespace AwsServer.Controllers.Requests;

public class InitializeQuickSightRequest
{
    public required AwsConfiguration Config { get; set; }
    public required string AwsAccountId { get; set; }
}