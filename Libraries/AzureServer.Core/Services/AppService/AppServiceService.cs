using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Services.AppService.Models;
using AzureServer.Core.Services.Core;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.AppService;

public class AppServiceService(
    ArmClientFactory armClientFactory,
    ILogger<AppServiceService> logger) : IAppServiceService
{
    #region Web App Operations

    public async Task<IEnumerable<WebAppDto>> ListWebAppsAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var webApps = new List<WebAppDto>();

            if (!string.IsNullOrEmpty(resourceGroupName) && !string.IsNullOrEmpty(subscriptionId))
            {
                // List web apps in a specific resource group
                var resourceGroup = armClient.GetResourceGroupResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));

                await foreach (var webApp in resourceGroup.GetWebSites())
                {
                    webApps.Add(await MapWebAppAsync(webApp));
                }
            }
            else if (!string.IsNullOrEmpty(subscriptionId))
            {
                // List web apps in a specific subscription
                var subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                await foreach (var webApp in subscription.GetWebSitesAsync())
                {
                    webApps.Add(await MapWebAppAsync(webApp));
                }
            }
            else
            {
                // List web apps across all subscriptions
                await foreach (var subscription in armClient.GetSubscriptions())
                {
                    await foreach (var webApp in subscription.GetWebSitesAsync())
                    {
                        webApps.Add(await MapWebAppAsync(webApp));
                    }
                }
            }

            logger.LogInformation("Retrieved {Count} web apps", webApps.Count);
            return webApps;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing web apps");
            throw;
        }
    }

    public async Task<WebAppDto?> GetWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                // Try to find in any subscription
                await foreach (var subscription in armClient.GetSubscriptions())
                {
                    try
                    {
                        ResourceGroupResource resourceGroup = await subscription.GetResourceGroups()
                            .GetAsync(resourceGroupName);
                        WebSiteResource webApp = await resourceGroup.GetWebSites().GetAsync(webAppName);
                        return await MapWebAppAsync(webApp);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                    }
                }
                return null;
            }

            var targetResourceGroup = armClient.GetResourceGroupResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));

            WebSiteResource targetWebApp = await targetResourceGroup.GetWebSites().GetAsync(webAppName);
            return await MapWebAppAsync(targetWebApp);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Web app {WebAppName} not found in resource group {ResourceGroupName}", webAppName, resourceGroupName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving web app {WebAppName}", webAppName);
            throw;
        }
    }

    public async Task<bool> StartWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;

            await webApp.StartAsync();
            logger.LogInformation("Started web app {WebAppName}", webAppName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting web app {WebAppName}", webAppName);
            throw;
        }
    }

    public async Task<bool> StopWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;

            await webApp.StopAsync();
            logger.LogInformation("Stopped web app {WebAppName}", webAppName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping web app {WebAppName}", webAppName);
            throw;
        }
    }

    public async Task<bool> RestartWebAppAsync(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;

            await webApp.RestartAsync();
            logger.LogInformation("Restarted web app {WebAppName}", webAppName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restarting web app {WebAppName}", webAppName);
            throw;
        }
    }

    #endregion

    #region Deployment Slot Operations

    public async Task<IEnumerable<DeploymentSlotDto>> ListDeploymentSlotsAsync(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return [];

            var slots = new List<DeploymentSlotDto>();
            await foreach (var slot in webApp.GetWebSiteSlots())
            {
                slots.Add(MapDeploymentSlot(slot));
            }

            logger.LogInformation("Retrieved {Count} deployment slots for {WebAppName}", slots.Count, webAppName);
            return slots;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing deployment slots for {WebAppName}", webAppName);
            throw;
        }
    }

    public async Task<DeploymentSlotDto?> GetDeploymentSlotAsync(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return null;

            WebSiteSlotResource slot = await webApp.GetWebSiteSlots().GetAsync(slotName);
            return MapDeploymentSlot(slot);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Deployment slot {SlotName} not found for web app {WebAppName}", slotName, webAppName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving deployment slot {SlotName} for {WebAppName}", slotName, webAppName);
            throw;
        }
    }

    public async Task<bool> SwapSlotsAsync(string webAppName, string sourceSlotName, string targetSlotName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;

            var swapParameters = new CsmSlotEntity(targetSlotName, true);

            if (sourceSlotName.Equals("production", StringComparison.OrdinalIgnoreCase))
            {
                await webApp.SwapSlotWithProductionAsync(WaitUntil.Completed, swapParameters);
            }
            else
            {
                WebSiteSlotResource sourceSlot = await webApp.GetWebSiteSlots().GetAsync(sourceSlotName);
                await sourceSlot.SwapSlotAsync(WaitUntil.Completed, swapParameters);
            }

            logger.LogInformation("Swapped slots {SourceSlot} -> {TargetSlot} for {WebAppName}", sourceSlotName, targetSlotName, webAppName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error swapping slots for {WebAppName}", webAppName);
            throw;
        }
    }

    public async Task<bool> StartSlotAsync(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;

            WebSiteSlotResource slot = await webApp.GetWebSiteSlots().GetAsync(slotName);
            await slot.StartSlotAsync();

            logger.LogInformation("Started deployment slot {SlotName} for {WebAppName}", slotName, webAppName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting slot {SlotName} for {WebAppName}", slotName, webAppName);
            throw;
        }
    }

    public async Task<bool> StopSlotAsync(string webAppName, string slotName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;

            WebSiteSlotResource slot = await webApp.GetWebSiteSlots().GetAsync(slotName);
            await slot.StopSlotAsync();

            logger.LogInformation("Stopped deployment slot {SlotName} for {WebAppName}", slotName, webAppName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping slot {SlotName} for {WebAppName}", slotName, webAppName);
            throw;
        }
    }

    #endregion

    #region Configuration Operations

    public async Task<AppSettingsDto> GetAppSettingsAsync(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return new AppSettingsDto();

            WebSiteConfigResource config = await webApp.GetWebSiteConfig().GetAsync();
            var settings = config.Data.AppSettings?.ToDictionary(x => x.Name, x => x.Value) ?? new Dictionary<string, string>();

            logger.LogInformation("Retrieved {Count} app settings for {WebAppName}", settings.Count, webAppName);
            return new AppSettingsDto { Settings = settings };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving app settings for {WebAppName}", webAppName);
            throw;
        }
    }

    public async Task<bool> UpdateAppSettingsAsync(string webAppName, string resourceGroupName, Dictionary<string, string> settings, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;
            
            var appSettings = new AppServiceConfigurationDictionary();
            foreach (var setting in settings)
            {
                appSettings.Properties[setting.Key] = setting.Value;
            }

            await webApp.UpdateApplicationSettingsAsync(appSettings);
            logger.LogInformation("Updated app settings for {WebAppName}", webAppName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating app settings for {WebAppName}", webAppName);
            throw;
        }
    }

    public async Task<IEnumerable<ConnectionStringDto>> GetConnectionStringsAsync(string webAppName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return [];

            Response<ConnectionStringDictionary>? connectionStrings = await webApp.GetConnectionStringsAsync();
            var result = connectionStrings.Value.Properties?
                .Select(kvp => new ConnectionStringDto
                {
                    Name = kvp.Key,
                    Value = kvp.Value.Value,
                    Type = kvp.Value.ConnectionStringType.ToString()
                })
                .ToList() ?? [];

            logger.LogInformation("Retrieved {Count} connection strings for {WebAppName}", result.Count, webAppName);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving connection strings for {WebAppName}", webAppName);
            throw;
        }
    }

    #endregion

    #region Scaling Operations
    
    public async Task<bool> ScaleWebAppAsync(string webAppName, string resourceGroupName, int instanceCount, string? subscriptionId = null)
    {
        try
        {
            var webApp = await GetWebAppResourceAsync(webAppName, resourceGroupName, subscriptionId);
            if (webApp is null)
                return false;

            // Get the app service plan
            string? planId = webApp.Data.AppServicePlanId;
            if (string.IsNullOrEmpty(planId))
            {
                logger.LogWarning("No app service plan found for {WebAppName}", webAppName);
                return false;
            }

            var armClient = await armClientFactory.GetArmClientAsync();
            var plan = armClient.GetAppServicePlanResource(new ResourceIdentifier(planId));

            // Get current plan data to preserve SKU settings
            var currentData = plan.Data;
            var planResourceGroup = plan.Id.ResourceGroupName ?? resourceGroupName;
            var planSubscriptionId = plan.Id.SubscriptionId ?? subscriptionId;
            
            // Get the resource group
            var rgResource = armClient.GetResourceGroupResource(
                new ResourceIdentifier($"/subscriptions/{planSubscriptionId}/resourceGroups/{planResourceGroup}"));
            
            var updateData = new AppServicePlanData(currentData.Location)
            {
                Sku = new AppServiceSkuDescription
                {
                    Name = currentData.Sku.Name,
                    Tier = currentData.Sku.Tier,
                    Size = currentData.Sku.Size,
                    Family = currentData.Sku.Family,
                    Capacity = instanceCount
                }
            };

            // Use CreateOrUpdate to update the capacity
            await rgResource.GetAppServicePlans().CreateOrUpdateAsync(WaitUntil.Completed, plan.Data.Name, updateData);
            logger.LogInformation("Scaled {WebAppName} to {InstanceCount} instances", webAppName, instanceCount);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error scaling web app {WebAppName}", webAppName);
            throw;
        }
    }

    #endregion

    #region App Service Plan Operations

    public async Task<IEnumerable<AppServicePlanDto>> ListAppServicePlansAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var plans = new List<AppServicePlanDto>();

            if (!string.IsNullOrEmpty(resourceGroupName) && !string.IsNullOrEmpty(subscriptionId))
            {
                // List plans in a specific resource group
                var resourceGroup = armClient.GetResourceGroupResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));

                await foreach (var plan in resourceGroup.GetAppServicePlans())
                {
                    plans.Add(MapAppServicePlan(plan));
                }
            }
            else if (!string.IsNullOrEmpty(subscriptionId))
            {
                // List plans in a specific subscription
                var subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                await foreach (var plan in subscription.GetAppServicePlansAsync())
                {
                    plans.Add(MapAppServicePlan(plan));
                }
            }
            else
            {
                // List plans across all subscriptions
                await foreach (var subscription in armClient.GetSubscriptions())
                {
                    await foreach (var plan in subscription.GetAppServicePlansAsync())
                    {
                        plans.Add(MapAppServicePlan(plan));
                    }
                }
            }

            logger.LogInformation("Retrieved {Count} app service plans", plans.Count);
            return plans;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing app service plans");
            throw;
        }
    }

    public async Task<AppServicePlanDto?> GetAppServicePlanAsync(string planName, string resourceGroupName, string? subscriptionId = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                // Try to find in any subscription
                await foreach (var subscription in armClient.GetSubscriptions())
                {
                    try
                    {
                        ResourceGroupResource resourceGroup = await subscription.GetResourceGroups()
                            .GetAsync(resourceGroupName);
                        AppServicePlanResource plan = await resourceGroup.GetAppServicePlans().GetAsync(planName);
                        return MapAppServicePlan(plan);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                    }
                }
                return null;
            }

            var targetResourceGroup = armClient.GetResourceGroupResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));

            AppServicePlanResource targetPlan = await targetResourceGroup.GetAppServicePlans().GetAsync(planName);
            return MapAppServicePlan(targetPlan);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("App service plan {PlanName} not found", planName);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving app service plan {PlanName}", planName);
            throw;
        }
    }

    #endregion

    #region Log Operations

    public async Task<string> GetApplicationLogsAsync(string webAppName, string resourceGroupName, int? lastHours = null, string? subscriptionId = null)
    {
        try
        {
            // Note: This is a simplified implementation
            // Full log streaming would require additional Azure SDK components
            logger.LogInformation("Retrieving logs for {WebAppName} (last {Hours} hours)", webAppName, lastHours ?? 24);
            
            return $"Log streaming for {webAppName} - This feature requires Kudu API integration or Azure Monitor Logs.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving logs for {WebAppName}", webAppName);
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private async Task<WebSiteResource?> GetWebAppResourceAsync(string webAppName, string resourceGroupName, string? subscriptionId)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                // Try to find in any subscription
                await foreach (var subscription in armClient.GetSubscriptions())
                {
                    try
                    {
                        ResourceGroupResource resourceGroup = await subscription.GetResourceGroups()
                            .GetAsync(resourceGroupName);
                        return await resourceGroup.GetWebSites().GetAsync(webAppName);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                    }
                }
                return null;
            }

            var targetResourceGroup = armClient.GetResourceGroupResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}"));

            return await targetResourceGroup.GetWebSites().GetAsync(webAppName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    #endregion

    #region Mapping Methods

    private async Task<WebAppDto> MapWebAppAsync(WebSiteResource webApp)
    {
        Response<WebSiteConfigResource>? config = await webApp.GetWebSiteConfig().GetAsync();
        
        return new WebAppDto
        {
            Id = webApp.Id.ToString(),
            Name = webApp.Data.Name,
            Location = webApp.Data.Location.Name,
            ResourceGroup = webApp.Id.ResourceGroupName ?? string.Empty,
            SubscriptionId = webApp.Id.SubscriptionId ?? string.Empty,
            Kind = webApp.Data.Kind ?? string.Empty,
            State = webApp.Data.State ?? string.Empty,
            DefaultHostName = webApp.Data.DefaultHostName,
            HostNames = webApp.Data.HostNames?.ToList(),
            RepositorySiteName = webApp.Data.RepositorySiteName,
            UsageState = webApp.Data.UsageState.ToString(),
            Enabled = webApp.Data.IsEnabled ?? false,
            EnabledHostNames = webApp.Data.EnabledHostNames?.ToList(),
            AvailabilityState = webApp.Data.AvailabilityState.ToString(),
            LastModifiedTimeUtc = webApp.Data.LastModifiedTimeUtc?.DateTime,
            OutboundIpAddresses = webApp.Data.OutboundIPAddresses,
            PossibleOutboundIpAddresses = webApp.Data.PossibleOutboundIPAddresses,
            AppServicePlanId = webApp.Data.AppServicePlanId,
            ServerFarmId = webApp.Data.AppServicePlanId,
            HttpsOnly = webApp.Data.IsHttpsOnly,
            ClientAffinityEnabled = webApp.Data.IsClientAffinityEnabled,
            ClientCertEnabled = webApp.Data.IsClientCertEnabled,
            ClientCertMode = webApp.Data.ClientCertMode?.ToString(),
            Tags = webApp.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            RuntimeStack = config.Value.Data.LinuxFxVersion ?? config.Value.Data.WindowsFxVersion,
            SiteUrl = $"https://{webApp.Data.DefaultHostName}"
        };
    }

    private static DeploymentSlotDto MapDeploymentSlot(WebSiteSlotResource slot)
    {
        return new DeploymentSlotDto
        {
            Id = slot.Id.ToString(),
            Name = slot.Data.Name,
            SlotName = slot.Id.Name,
            Location = slot.Data.Location.Name,
            ResourceGroup = slot.Id.ResourceGroupName ?? string.Empty,
            State = slot.Data.State ?? string.Empty,
            DefaultHostName = slot.Data.DefaultHostName,
            Enabled = slot.Data.IsEnabled ?? false,
            AvailabilityState = slot.Data.AvailabilityState.ToString(),
            LastModifiedTimeUtc = slot.Data.LastModifiedTimeUtc?.DateTime,
            Tags = slot.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    private static AppServicePlanDto MapAppServicePlan(AppServicePlanResource plan)
    {
        return new AppServicePlanDto
        {
            Id = plan.Id.ToString(),
            Name = plan.Data.Name,
            Location = plan.Data.Location.Name,
            ResourceGroup = plan.Id.ResourceGroupName ?? string.Empty,
            SubscriptionId = plan.Id.SubscriptionId ?? string.Empty,
            SkuName = plan.Data.Sku?.Name,
            SkuTier = plan.Data.Sku?.Tier,
            SkuCapacity = plan.Data.Sku?.Capacity,
            Status = plan.Data.Status.ToString(),
            NumberOfSites = plan.Data.NumberOfSites,
            MaximumNumberOfWorkers = plan.Data.MaximumNumberOfWorkers,
            IsSpot = plan.Data.IsSpot,
            HyperV = plan.Data.IsHyperV,
            Tags = plan.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    #endregion
}
