namespace AwsServer.Controllers.Requests;

public class GenerateEmbedUrlRequest
{
    public string Namespace { get; set; } = "default";
    public List<string>? AuthorizedResourceArns { get; set; }
    public string? Region { get; set; }
    public long SessionLifetimeInMinutes { get; set; } = 600;
}