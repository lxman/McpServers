using AwsServer.Core.Models.Controllers.Models;

namespace AwsServer.Core.Models.Responses;

public class HealthStatusResponse
{
    public required string Status { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, ServiceStatus> Services { get; set; } = new();
    public int HealthyServices { get; set; }
    public int TotalServices { get; set; }
    public int HealthPercentage { get; set; }
}