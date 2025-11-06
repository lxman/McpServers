namespace AwsServer.Core.Models.Requests;

public class PutObjectRequest
{
    public required string Content { get; set; }
    public string? ContentType { get; set; }
}