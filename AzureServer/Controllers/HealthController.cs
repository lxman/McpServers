using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Authentication;
using AzureServer.Core.Common.Models;
using AzureServer.Core.Services.AppService;
using AzureServer.Core.Services.Container;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.CostManagement;
using AzureServer.Core.Services.DevOps;
using AzureServer.Core.Services.EventHubs;
using AzureServer.Core.Services.KeyVault;
using AzureServer.Core.Services.Monitor;
using AzureServer.Core.Services.ResourceManagement;
using AzureServer.Core.Services.ServiceBus;
using AzureServer.Core.Services.Sql.DbManagement;
using AzureServer.Core.Services.Sql.QueryExecution;
using AzureServer.Core.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

/// <summary>
/// Health Check and Account Information API
/// </summary>
[ApiController]
[Route("api")]
public class HealthController(
    ArmClientFactory armClientFactory,
    CredentialSelectionService credentialService,
    ILogger<HealthController> logger)
    : ControllerBase
{
    /// <summary>
    /// Get overall service health and credential status.
    /// Returns status of authentication and available Azure services.
    /// 
    /// GET /api/health
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(AzureResponse<HealthStatusResponse>), 200)]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            DateTime startTime = DateTime.UtcNow;
            
            // Check if we can get credentials
            var credentialsAvailable = false;
            string? credentialStatus = null;
            CredentialInfo? selectedCredential = null;
            
            try
            {
                (_, CredentialSelectionResult result) = await credentialService.GetCredentialAsync();
                credentialsAvailable = result.Status == SelectionStatus.Selected || 
                                      result.Status == SelectionStatus.AutoSelected;
                credentialStatus = result.Status.ToString();
                selectedCredential = result.SelectedCredential;
            }
            catch (Exception ex)
            {
                credentialStatus = $"Error: {ex.Message}";
            }
            
            // Check if we can create an ArmClient
            var armClientAvailable = false;
            try
            {
                ArmClient? armClient = await armClientFactory.GetArmClientAsync();
                armClientAvailable = armClient is not null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create ArmClient during health check");
            }
            
            // Determine overall status
            string overallStatus = credentialsAvailable && armClientAvailable ? "Healthy" : "Unhealthy";
            
            var serviceStatuses = new Dictionary<string, ServiceStatus>
            {
                ["credentials"] = new()
                {
                    ServiceName = "Azure Credentials",
                    IsAvailable = credentialsAvailable,
                    Status = credentialStatus ?? "Unknown",
                    Details = selectedCredential != null 
                        ? $"{selectedCredential.Source} ({selectedCredential.AccountName})"
                        : null
                },
                ["arm-client"] = new()
                {
                    ServiceName = "Azure Resource Manager",
                    IsAvailable = armClientAvailable,
                    Status = armClientAvailable ? "Available" : "Unavailable"
                },
                ["monitor"] = new()
                {
                    ServiceName = "Azure Monitor (Logs & Metrics)",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["storage"] = new()
                {
                    ServiceName = "Azure Storage",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["sql"] = new()
                {
                    ServiceName = "Azure SQL",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["key-vault"] = new()
                {
                    ServiceName = "Azure Key Vault",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["app-service"] = new()
                {
                    ServiceName = "Azure App Service",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["container"] = new()
                {
                    ServiceName = "Azure Container Services",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["networking"] = new()
                {
                    ServiceName = "Azure Networking",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["service-bus"] = new()
                {
                    ServiceName = "Azure Service Bus",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["event-hubs"] = new()
                {
                    ServiceName = "Azure Event Hubs",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["devops"] = new()
                {
                    ServiceName = "Azure DevOps",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Check DevOps-specific auth" : "Requires Authentication",
                    Details = "DevOps may require separate PAT authentication"
                },
                ["cost-management"] = new()
                {
                    ServiceName = "Azure Cost Management",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["resource-management"] = new()
                {
                    ServiceName = "Azure Resource Management",
                    IsAvailable = credentialsAvailable,
                    Status = credentialsAvailable ? "Available" : "Requires Authentication"
                }
            };
            
            int availableServices = serviceStatuses.Count(s => s.Value.IsAvailable);
            int totalServices = serviceStatuses.Count;
            
            var response = new HealthStatusResponse
            {
                Status = overallStatus,
                Timestamp = DateTime.UtcNow,
                Services = serviceStatuses,
                AvailableServices = availableServices,
                TotalServices = totalServices,
                HealthPercentage = (int)((double)availableServices / totalServices * 100)
            };
            
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            
            return Ok(AzureResponse<HealthStatusResponse>.Ok(response, new ResponseMetadata
            {
                DurationMs = duration
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting health status");
            return StatusCode(500, AzureResponse<HealthStatusResponse>.Fail(
                ex.Message, 
                ex.GetType().Name));
        }
    }
    
    /// <summary>
    /// Get detailed information about current Azure credentials and tenant/subscription context.
    /// 
    /// GET /api/account-info
    /// </summary>
    [HttpGet("account-info")]
    [ProducesResponseType(typeof(AzureResponse<AccountInfoResponse>), 200)]
    public async Task<IActionResult> GetAccountInfo()
    {
        try
        {
            DateTime startTime = DateTime.UtcNow;
            
            (_, CredentialSelectionResult result) = await credentialService.GetCredentialAsync();
            
            if (result.Status == SelectionStatus.NoCredentialsFound)
            {
                return Ok(AzureResponse<AccountInfoResponse>.Fail(
                    "No Azure credentials found. Please authenticate using Azure CLI, Visual Studio, or environment variables.",
                    "NoCredentials"));
            }
            
            CredentialInfo? credential = result.SelectedCredential;
            if (credential == null)
            {
                return Ok(AzureResponse<AccountInfoResponse>.Fail(
                    "No credential selected",
                    "NoSelection"));
            }
            
            // Get subscription information
            List<SubscriptionInfo> subscriptions = new();
            try
            {
                ArmClient armClient = await armClientFactory.GetArmClientAsync();
                await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                {
                    subscriptions.Add(new SubscriptionInfo
                    {
                        SubscriptionId = subscription.Data.SubscriptionId,
                        DisplayName = subscription.Data.DisplayName,
                        State = subscription.Data.State?.ToString() ?? "Unknown",
                        TenantId = subscription.Data.TenantId?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to enumerate subscriptions");
            }
            
            var accountInfo = new AccountInfoResponse
            {
                CredentialSource = credential.Source ?? "Unknown",
                CredentialId = credential.Id ?? "Unknown",
                AccountName = credential.AccountName ?? "Unknown",
                TenantId = credential.TenantId ?? "Unknown",
                TenantName = credential.TenantName,
                SubscriptionCount = credential.SubscriptionCount,
                Subscriptions = subscriptions
            };
            
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            
            return Ok(AzureResponse<AccountInfoResponse>.Ok(accountInfo, new ResponseMetadata
            {
                DurationMs = duration,
                ItemCount = subscriptions.Count
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting account info");
            return StatusCode(500, AzureResponse<AccountInfoResponse>.Fail(
                ex.Message, 
                ex.GetType().Name));
        }
    }
}

/// <summary>
/// Health status response
/// </summary>
public class HealthStatusResponse
{
    public required string Status { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, ServiceStatus> Services { get; set; } = new();
    public int AvailableServices { get; set; }
    public int TotalServices { get; set; }
    public int HealthPercentage { get; set; }
}

/// <summary>
/// Individual service status
/// </summary>
public class ServiceStatus
{
    public required string ServiceName { get; set; }
    public bool IsAvailable { get; set; }
    public required string Status { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// Azure account information response
/// </summary>
public class AccountInfoResponse
{
    public required string CredentialSource { get; set; }
    public required string CredentialId { get; set; }
    public required string AccountName { get; set; }
    public required string TenantId { get; set; }
    public string? TenantName { get; set; }
    public int SubscriptionCount { get; set; }
    public List<SubscriptionInfo> Subscriptions { get; set; } = new();
}

/// <summary>
/// Information about an Azure subscription
/// </summary>
public class SubscriptionInfo
{
    public required string SubscriptionId { get; set; }
    public required string DisplayName { get; set; }
    public required string State { get; set; }
    public string? TenantId { get; set; }
}