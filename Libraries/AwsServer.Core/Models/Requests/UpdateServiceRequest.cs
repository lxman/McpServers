namespace AwsServer.Core.Models.Requests;

public class UpdateServiceRequest
{
    public int? DesiredCount { get; set; }
    public string? TaskDefinition { get; set; }
}