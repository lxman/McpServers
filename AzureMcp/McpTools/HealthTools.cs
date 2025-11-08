using System.ComponentModel;
using System.Text.Json;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Authentication;
using AzureServer.Core.Services.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure health and account information
/// </summary>
[McpServerToolType]
public class HealthTools(
    ArmClientFactory armClientFactory,
    CredentialSelectionService credentialService,
    ILogger<HealthTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("get_health")]
    [Description("Get Azure service health status. See skills/azure/health/get-health.md only when using this tool")]
    public async Task<string> GetHealth()
    {
        try
        {
            logger.LogDebug("Getting health status");

            // Check if we can get credentials
            var credentialsAvailable = false;
            string? credentialStatus = null;
            CredentialInfo? selectedCredential = null;

            try
            {
                (_, var result) = await credentialService.GetCredentialAsync();
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
                var armClient = await armClientFactory.GetArmClientAsync();
                armClientAvailable = armClient is not null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create ArmClient during health check");
            }

            // Determine overall status
            var overallStatus = credentialsAvailable && armClientAvailable ? "Healthy" : "Unhealthy";

            var serviceStatuses = new Dictionary<string, object>
            {
                ["credentials"] = new
                {
                    serviceName = "Azure Credentials",
                    isAvailable = credentialsAvailable,
                    status = credentialStatus ?? "Unknown",
                    details = selectedCredential != null
                        ? $"{selectedCredential.Source} ({selectedCredential.AccountName})"
                        : null
                },
                ["arm-client"] = new
                {
                    serviceName = "Azure Resource Manager",
                    isAvailable = armClientAvailable,
                    status = armClientAvailable ? "Available" : "Unavailable"
                },
                ["monitor"] = new
                {
                    serviceName = "Azure Monitor (Logs & Metrics)",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["storage"] = new
                {
                    serviceName = "Azure Storage",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["sql"] = new
                {
                    serviceName = "Azure SQL",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["key-vault"] = new
                {
                    serviceName = "Azure Key Vault",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["app-service"] = new
                {
                    serviceName = "Azure App Service",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["container"] = new
                {
                    serviceName = "Azure Container Services",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["networking"] = new
                {
                    serviceName = "Azure Networking",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["service-bus"] = new
                {
                    serviceName = "Azure Service Bus",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["event-hubs"] = new
                {
                    serviceName = "Azure Event Hubs",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["devops"] = new
                {
                    serviceName = "Azure DevOps",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Check DevOps-specific auth" : "Requires Authentication",
                    details = "DevOps may require separate PAT authentication"
                },
                ["cost-management"] = new
                {
                    serviceName = "Azure Cost Management",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                },
                ["resource-management"] = new
                {
                    serviceName = "Azure Resource Management",
                    isAvailable = credentialsAvailable,
                    status = credentialsAvailable ? "Available" : "Requires Authentication"
                }
            };

            var availableServices = serviceStatuses.Count(s => ((dynamic)s.Value).isAvailable);
            var totalServices = serviceStatuses.Count;

            return JsonSerializer.Serialize(new
            {
                success = true,
                status = overallStatus,
                timestamp = DateTime.UtcNow,
                services = serviceStatuses,
                availableServices,
                totalServices,
                healthPercentage = (int)((double)availableServices / totalServices * 100)
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting health status");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_account_info")]
    [Description("Get Azure account information. See skills/azure/health/get-account-info.md only when using this tool")]
    public async Task<string> GetAccountInfo()
    {
        try
        {
            logger.LogDebug("Getting account information");

            (_, var result) = await credentialService.GetCredentialAsync();

            if (result.Status == SelectionStatus.NoCredentialsFound)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No Azure credentials found. Please authenticate using Azure CLI, Visual Studio, or environment variables."
                }, _jsonOptions);
            }

            var credential = result.SelectedCredential;
            if (credential == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No credential selected"
                }, _jsonOptions);
            }

            // Get subscription information
            var subscriptions = new List<object>();
            try
            {
                var armClient = await armClientFactory.GetArmClientAsync();
                await foreach (var subscription in armClient.GetSubscriptions())
                {
                    subscriptions.Add(new
                    {
                        subscriptionId = subscription.Data.SubscriptionId,
                        displayName = subscription.Data.DisplayName,
                        state = subscription.Data.State?.ToString() ?? "Unknown",
                        tenantId = subscription.Data.TenantId?.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to enumerate subscriptions");
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                accountInfo = new
                {
                    credentialSource = credential.Source ?? "Unknown",
                    credentialId = credential.Id ?? "Unknown",
                    accountName = credential.AccountName ?? "Unknown",
                    tenantId = credential.TenantId ?? "Unknown",
                    tenantName = credential.TenantName,
                    subscriptionCount = credential.SubscriptionCount,
                    subscriptions
                }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting account info");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }
}