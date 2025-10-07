using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.AppService;
using AzureMcp.Services.AppService.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class AppServiceTools(IAppServiceService appServiceService)
{
    #region Web App Operations

    [McpServerTool]
    [Description("List Azure App Service web apps")]
    public async Task<string> ListWebAppsAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<WebAppDto> webApps = await appServiceService.ListWebAppsAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, webApps = webApps.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListWebApps");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific web app")]
    public async Task<string> GetWebAppAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            WebAppDto? webApp = await appServiceService.GetWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Web app {webAppName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, webApp },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetWebApp");
        }
    }

    [McpServerTool]
    [Description("Start a web app")]
    public async Task<string> StartWebAppAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StartWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? $"Web app {webAppName} started" : $"Failed to start web app {webAppName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StartWebApp");
        }
    }

    [McpServerTool]
    [Description("Stop a web app")]
    public async Task<string> StopWebAppAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StopWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? $"Web app {webAppName} stopped" : $"Failed to stop web app {webAppName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StopWebApp");
        }
    }

    [McpServerTool]
    [Description("Restart a web app")]
    public async Task<string> RestartWebAppAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.RestartWebAppAsync(webAppName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? $"Web app {webAppName} restarted" : $"Failed to restart web app {webAppName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "RestartWebApp");
        }
    }

    #endregion

    #region Deployment Slot Operations

    [McpServerTool]
    [Description("List deployment slots for a web app")]
    public async Task<string> ListDeploymentSlotsAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<DeploymentSlotDto> slots = await appServiceService.ListDeploymentSlotsAsync(webAppName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, slots = slots.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListDeploymentSlots");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific deployment slot")]
    public async Task<string> GetDeploymentSlotAsync(
        [Description("Web app name")] string webAppName,
        [Description("Slot name")] string slotName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            DeploymentSlotDto? slot = await appServiceService.GetDeploymentSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);
            if (slot is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Deployment slot {slotName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, slot },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetDeploymentSlot");
        }
    }

    [McpServerTool]
    [Description("Swap deployment slots (e.g., staging to production)")]
    public async Task<string> SwapSlotsAsync(
        [Description("Web app name")] string webAppName,
        [Description("Source slot name (e.g., 'staging' or 'production')")] string sourceSlotName,
        [Description("Target slot name (e.g., 'production' or 'staging')")] string targetSlotName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.SwapSlotsAsync(webAppName, sourceSlotName, targetSlotName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? $"Swapped {sourceSlotName} -> {targetSlotName}" : "Swap failed" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "SwapSlots");
        }
    }

    [McpServerTool]
    [Description("Start a deployment slot")]
    public async Task<string> StartSlotAsync(
        [Description("Web app name")] string webAppName,
        [Description("Slot name")] string slotName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StartSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? $"Slot {slotName} started" : "Start failed" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StartSlot");
        }
    }

    [McpServerTool]
    [Description("Stop a deployment slot")]
    public async Task<string> StopSlotAsync(
        [Description("Web app name")] string webAppName,
        [Description("Slot name")] string slotName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            bool success = await appServiceService.StopSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? $"Slot {slotName} stopped" : "Stop failed" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StopSlot");
        }
    }

    #endregion

    #region Configuration Operations

    [McpServerTool]
    [Description("Get application settings for a web app")]
    public async Task<string> GetAppSettingsAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            AppSettingsDto settings = await appServiceService.GetAppSettingsAsync(webAppName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, settings = settings.Settings },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetAppSettings");
        }
    }

    [McpServerTool]
    [Description("Update application settings for a web app")]
    public async Task<string> UpdateAppSettingsAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Settings as JSON object (e.g., '{\"Setting1\":\"Value1\",\"Setting2\":\"Value2\"}')")] string settingsJson,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);
            if (settings is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid settings JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            bool success = await appServiceService.UpdateAppSettingsAsync(webAppName, resourceGroupName, settings, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? "Settings updated" : "Update failed" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateAppSettings");
        }
    }

    [McpServerTool]
    [Description("Get connection strings for a web app")]
    public async Task<string> GetConnectionStringsAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ConnectionStringDto> connectionStrings = await appServiceService.GetConnectionStringsAsync(webAppName, resourceGroupName, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, connectionStrings = connectionStrings.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetConnectionStrings");
        }
    }

    #endregion

    #region Scaling Operations

    [McpServerTool]
    [Description("Scale a web app to a specific number of instances")]
    public async Task<string> ScaleWebAppAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Number of instances (1-30)")] int instanceCount,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            if (instanceCount < 1 || instanceCount > 30)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Instance count must be between 1 and 30" },
                    SerializerOptions.JsonOptionsIndented);
            }

            bool success = await appServiceService.ScaleWebAppAsync(webAppName, resourceGroupName, instanceCount, subscriptionId);
            return JsonSerializer.Serialize(new { success, message = success ? $"Scaled to {instanceCount} instances" : "Scaling failed" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ScaleWebApp");
        }
    }

    #endregion

    #region App Service Plan Operations

    [McpServerTool]
    [Description("List Azure App Service plans")]
    public async Task<string> ListAppServicePlansAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<AppServicePlanDto> plans = await appServiceService.ListAppServicePlansAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, plans = plans.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListAppServicePlans");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific app service plan")]
    public async Task<string> GetAppServicePlanAsync(
        [Description("App service plan name")] string planName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            AppServicePlanDto? plan = await appServiceService.GetAppServicePlanAsync(planName, resourceGroupName, subscriptionId);
            if (plan is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"App service plan {planName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, plan },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetAppServicePlan");
        }
    }

    #endregion

    #region Log Operations

    [McpServerTool]
    [Description("Get application logs for a web app")]
    public async Task<string> GetApplicationLogsAsync(
        [Description("Web app name")] string webAppName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Optional: Number of hours to retrieve logs (default: 24)")] int? lastHours = null,
        [Description("Optional subscription ID")] string? subscriptionId = null)
    {
        try
        {
            string logs = await appServiceService.GetApplicationLogsAsync(webAppName, resourceGroupName, lastHours, subscriptionId);
            return JsonSerializer.Serialize(new { success = true, logs, note = "Full log streaming requires Kudu API or Azure Monitor integration" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetApplicationLogs");
        }
    }

    #endregion

    #region Error Handling

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            operation,
            error = ex.Message,
            type = ex.GetType().Name
        }, SerializerOptions.JsonOptionsIndented);
    }

    #endregion
}
