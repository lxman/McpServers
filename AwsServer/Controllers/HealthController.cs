using AwsServer.CloudWatch;
using AwsServer.Configuration;
using AwsServer.Configuration.Models;
using AwsServer.Controllers.Models;
using AwsServer.Controllers.Responses;
using AwsServer.ECR;
using AwsServer.ECS;
using AwsServer.QuickSight;
using AwsServer.S3;
using Microsoft.AspNetCore.Mvc;

namespace AwsServer.Controllers;

/// <summary>
/// Health Check and Service Status API
/// </summary>
[ApiController]
[Route("api")]
public class HealthController(
    CloudWatchLogsService logsService,
    CloudWatchMetricsService metricsService,
    AwsDiscoveryService discoveryService,
    EcrService ecrService,
    EcsService ecsService,
    QuickSightService quickSightService,
    S3Service s3Service,
    ILogger<HealthController> logger)
    : ControllerBase
{
    /// <summary>
    /// Get overall service health and initialization status.
    /// Returns status of all AWS services (Logs, Metrics, ECR, ECS, etc.)
    /// 
    /// GET /api/health
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthStatusResponse), 200)]
    public IActionResult GetHealth()
    {
        try
        {
            var serviceStatuses = new Dictionary<string, ServiceStatus>
            {
                ["cloudwatch-logs"] = new()
                {
                    ServiceName = "CloudWatch Logs",
                    IsInitialized = logsService.IsInitialized,
                    Status = logsService.IsInitialized ? "Healthy" : "Not Initialized"
                },
                ["cloudwatch-metrics"] = new()
                {
                    ServiceName = "CloudWatch Metrics",
                    IsInitialized = metricsService.IsInitialized,
                    Status = metricsService.IsInitialized ? "Healthy" : "Not Initialized"
                },
                ["ecr"] = new()
                {
                    ServiceName = "Elastic Container Registry",
                    IsInitialized = ecrService.IsInitialized,
                    Status = ecrService.IsInitialized ? "Healthy" : "Not Initialized"
                },
                ["ecs"] = new()
                {
                    ServiceName = "Elastic Container Service",
                    IsInitialized = ecsService.IsInitialized,
                    Status = ecsService.IsInitialized ? "Healthy" : "Not Initialized"
                },
                ["quicksight"] = new()
                {
                    ServiceName = "QuickSight",
                    IsInitialized = quickSightService.IsInitialized,
                    Status = quickSightService.IsInitialized ? "Healthy" : "Not Initialized"
                },
                ["s3"] = new()
                {
                    ServiceName = "S3",
                    IsInitialized = s3Service.IsInitialized,
                    Status = s3Service.IsInitialized ? "Healthy" : "Not Initialized"
                }
            };

            int healthyServices = serviceStatuses.Count(s => s.Value.IsInitialized);
            int totalServices = serviceStatuses.Count;

            string overallStatus = healthyServices == totalServices ? "Healthy" 
                : healthyServices > 0 ? "Degraded" 
                : "Unhealthy";

            return Ok(new HealthStatusResponse
            {
                Status = overallStatus,
                Timestamp = DateTime.UtcNow,
                Services = serviceStatuses,
                HealthyServices = healthyServices,
                TotalServices = totalServices,
                HealthPercentage = (int)((double)healthyServices / totalServices * 100)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting health status");
            return StatusCode(500, new
            {
                status = "Error",
                error = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Get detailed information about AWS account configuration.
    /// 
    /// GET /api/account-info
    /// </summary>
    [HttpGet("account-info")]
    [ProducesResponseType(typeof(AccountInfoResponse), 200)]
    public async Task<IActionResult> GetAccountInfo()
    {
        try
        {
            AccountInfo accountInfo = await discoveryService.GetAccountInfoAsync();
            
            return Ok(new AccountInfoResponse
            {
                AccountId = accountInfo.AccountId,
                Region = accountInfo.InferredRegion,
                Arn = accountInfo.Arn,
                UserId = accountInfo.UserId,
                ProfileName = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting account info");
            return StatusCode(500, new { error = ex.Message, type = ex.GetType().Name });
        }
    }
}