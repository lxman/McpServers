namespace AwsServer.Core.Models.Requests;

public class RunTaskRequest
{
    public required string TaskDefinition { get; set; }
    public int Count { get; set; } = 1;
}