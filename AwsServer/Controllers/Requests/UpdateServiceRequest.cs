namespace AwsServer.Controllers.Requests;

public class UpdateServiceRequest
{
    public int? DesiredCount { get; set; }
    public string? TaskDefinition { get; set; }
}