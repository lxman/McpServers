using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using AzureServer.Core.Services.AppService;
using AzureServer.Core.Services.AppService.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure App Service operations
/// </summary>
[McpServerToolType]
public class AppServiceTools(
    IAppServiceService appServiceService,
    ILogger<AppServiceTools> logger)
{
    #region Web Apps

    [McpServerTool, DisplayName("list_webapps")]
    [Description("List web apps. See skills/azure/appservice/list-webapps.md only when using this tool")]
    public async Task<string> ListWebApps(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing web apps");
            IEnumerable<WebAppDto> webApps = await appServiceService.ListWebAppsAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                webApps = webApps.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing web apps");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_webapp")]
    [Description("Get web app. See skills/azure/appservice/get-webapp.md only when using this tool")]
    public async Task<string> GetWebApp(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting web app {WebAppName}", webAppName);
            WebAppDto? webApp = await appServiceService.GetWebAppAsync(webAppName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                webApp
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting web app {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("start_webapp")]
    [Description("Start web app. See skills/azure/appservice/start-webapp.md only when using this tool")]
    public async Task<string> StartWebApp(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Starting web app {WebAppName}", webAppName);
            bool success = await appServiceService.StartWebAppAsync(webAppName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Web app {webAppName} started" : $"Failed to start web app {webAppName}"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting web app {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("stop_webapp")]
    [Description("Stop web app. See skills/azure/appservice/stop-webapp.md only when using this tool")]
    public async Task<string> StopWebApp(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Stopping web app {WebAppName}", webAppName);
            bool success = await appServiceService.StopWebAppAsync(webAppName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Web app {webAppName} stopped" : $"Failed to stop web app {webAppName}"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping web app {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("restart_webapp")]
    [Description("Restart web app. See skills/azure/appservice/restart-webapp.md only when using this tool")]
    public async Task<string> RestartWebApp(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Restarting web app {WebAppName}", webAppName);
            bool success = await appServiceService.RestartWebAppAsync(webAppName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Web app {webAppName} restarted" : $"Failed to restart web app {webAppName}"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restarting web app {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Deployment Slots

    [McpServerTool, DisplayName("list_deployment_slots")]
    [Description("List deployment slots. See skills/azure/appservice/list-slots.md only when using this tool")]
    public async Task<string> ListDeploymentSlots(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing deployment slots for {WebAppName}", webAppName);
            IEnumerable<DeploymentSlotDto> slots = await appServiceService.ListDeploymentSlotsAsync(webAppName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                slots = slots.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing deployment slots for {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_deployment_slot")]
    [Description("Get deployment slot. See skills/azure/appservice/get-slot.md only when using this tool")]
    public async Task<string> GetDeploymentSlot(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting deployment slot {SlotName}", slotName);
            DeploymentSlotDto? slot = await appServiceService.GetDeploymentSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                slot
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting deployment slot {SlotName}", slotName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("swap_slots")]
    [Description("Swap deployment slots. See skills/azure/appservice/swap-slots.md only when using this tool")]
    public async Task<string> SwapSlots(
        string webAppName,
        string resourceGroupName,
        string sourceSlotName,
        string targetSlotName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Swapping slots {Source} -> {Target}", sourceSlotName, targetSlotName);
            bool success = await appServiceService.SwapSlotsAsync(webAppName, sourceSlotName, targetSlotName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Swapped {sourceSlotName} -> {targetSlotName}" : "Swap failed"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error swapping slots for {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("start_slot")]
    [Description("Start deployment slot. See skills/azure/appservice/start-slot.md only when using this tool")]
    public async Task<string> StartSlot(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Starting slot {SlotName}", slotName);
            bool success = await appServiceService.StartSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Slot {slotName} started" : "Start failed"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting slot {SlotName}", slotName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("stop_slot")]
    [Description("Stop deployment slot. See skills/azure/appservice/stop-slot.md only when using this tool")]
    public async Task<string> StopSlot(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Stopping slot {SlotName}", slotName);
            bool success = await appServiceService.StopSlotAsync(webAppName, slotName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Slot {slotName} stopped" : "Stop failed"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping slot {SlotName}", slotName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Configuration

    [McpServerTool, DisplayName("get_app_settings")]
    [Description("Get app settings. See skills/azure/appservice/get-app-settings.md only when using this tool")]
    public async Task<string> GetAppSettings(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting app settings for {WebAppName}", webAppName);
            AppSettingsDto settings = await appServiceService.GetAppSettingsAsync(webAppName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                settings = settings.Settings
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting app settings for {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("update_app_settings")]
    [Description("Update app settings. See skills/azure/appservice/update-app-settings.md only when using this tool")]
    public async Task<string> UpdateAppSettings(
        string webAppName,
        string resourceGroupName,
        Dictionary<string, string> settings,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Updating app settings for {WebAppName}", webAppName);
            bool success = await appServiceService.UpdateAppSettingsAsync(webAppName, resourceGroupName, settings, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? "Settings updated" : "Update failed"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating app settings for {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_connection_strings")]
    [Description("Get connection strings. See skills/azure/appservice/get-connection-strings.md only when using this tool")]
    public async Task<string> GetConnectionStrings(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting connection strings for {WebAppName}", webAppName);
            IEnumerable<ConnectionStringDto> connectionStrings = await appServiceService.GetConnectionStringsAsync(webAppName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                connectionStrings = connectionStrings.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting connection strings for {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Scaling

    [McpServerTool, DisplayName("scale_webapp")]
    [Description("Scale web app. See skills/azure/appservice/scale-webapp.md only when using this tool")]
    public async Task<string> ScaleWebApp(
        string webAppName,
        string resourceGroupName,
        int instanceCount,
        string? subscriptionId = null)
    {
        try
        {
            if (instanceCount is < 1 or > 30)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Instance count must be between 1 and 30"
                }, SerializerOptions.JsonOptionsIndented);
            }

            logger.LogDebug("Scaling web app {WebAppName} to {Count} instances", webAppName, instanceCount);
            bool success = await appServiceService.ScaleWebAppAsync(webAppName, resourceGroupName, instanceCount, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Scaled to {instanceCount} instances" : "Scaling failed"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scaling web app {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region App Service Plans

    [McpServerTool, DisplayName("list_app_service_plans")]
    [Description("List app service plans. See skills/azure/appservice/list-plans.md only when using this tool")]
    public async Task<string> ListAppServicePlans(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing app service plans");
            IEnumerable<AppServicePlanDto> plans = await appServiceService.ListAppServicePlansAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                plans = plans.ToArray()
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing app service plans");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    [McpServerTool, DisplayName("get_app_service_plan")]
    [Description("Get app service plan. See skills/azure/appservice/get-plan.md only when using this tool")]
    public async Task<string> GetAppServicePlan(string planName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting app service plan {PlanName}", planName);
            AppServicePlanDto? plan = await appServiceService.GetAppServicePlanAsync(planName, resourceGroupName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                plan
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting app service plan {PlanName}", planName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion

    #region Logs

    [McpServerTool, DisplayName("get_application_logs")]
    [Description("Get application logs. See skills/azure/appservice/get-logs.md only when using this tool")]
    public async Task<string> GetApplicationLogs(
        string webAppName,
        string resourceGroupName,
        int? lastHours = null,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting application logs for {WebAppName}", webAppName);
            string logs = await appServiceService.GetApplicationLogsAsync(webAppName, resourceGroupName, lastHours, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                logs,
                note = "Full log streaming requires Kudu API or Azure Monitor integration"
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting logs for {WebAppName}", webAppName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SerializerOptions.JsonOptionsIndented);
        }
    }

    #endregion
}