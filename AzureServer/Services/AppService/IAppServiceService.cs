using AzureServer.Services.AppService.Models;

namespace AzureServer.Services.AppService;

public interface IAppServiceService
{
    // Web App Operations
    Task<IEnumerable<WebAppDto>> ListWebAppsAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<WebAppDto?> GetWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> StartWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> StopWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> RestartWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null);

    // Deployment Slot Operations
    Task<IEnumerable<DeploymentSlotDto>> ListDeploymentSlotsAsync(string webAppName, string resourceGroupName, string? subscriptionId = null);
    Task<DeploymentSlotDto?> GetDeploymentSlotAsync(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> SwapSlotsAsync(string webAppName, string sourceSlotName, string targetSlotName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> StartSlotAsync(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> StopSlotAsync(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null);

    // Configuration Operations
    Task<AppSettingsDto> GetAppSettingsAsync(string webAppName, string resourceGroupName, string? subscriptionId = null);
    Task<bool> UpdateAppSettingsAsync(string webAppName, string resourceGroupName, Dictionary<string, string> settings, string? subscriptionId = null);
    Task<IEnumerable<ConnectionStringDto>> GetConnectionStringsAsync(string webAppName, string resourceGroupName, string? subscriptionId = null);

    // Scaling Operations
    Task<bool> ScaleWebAppAsync(string webAppName, string resourceGroupName, int instanceCount, string? subscriptionId = null);

    // App Service Plan Operations
    Task<IEnumerable<AppServicePlanDto>> ListAppServicePlansAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<AppServicePlanDto?> GetAppServicePlanAsync(string planName, string resourceGroupName, string? subscriptionId = null);

    // Log Operations
    Task<string> GetApplicationLogsAsync(string webAppName, string resourceGroupName, int? lastHours = null, string? subscriptionId = null);
}
