namespace AwsServer.Core.Models.Controllers.Models;

public class ServiceStatus
{
    public required string ServiceName { get; set; }
    public bool IsInitialized { get; set; }
    public required string Status { get; set; }
    public string? Message { get; set; }
}