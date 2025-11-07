using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.ResourceManagement;
using AzureServer.Core.Services.ResourceManagement.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Resource Management operations
/// </summary>
[McpServerToolType]
public class ResourceManagementTools(
    IResourceManagementService resourceService,
    ILogger<ResourceManagementTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_subscriptions")]
    [Description("List Azure subscriptions. See skills/azure/resourcemanagement/list-subscriptions.md only when using this tool")]
    public async Task<string> ListSubscriptions()
    {
        try
        {
            logger.LogDebug("Listing subscriptions");
            IEnumerable<SubscriptionDto> subscriptions = await resourceService.GetSubscriptionsAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscriptions = subscriptions.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing subscriptions");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListSubscriptions",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_subscription")]
    [Description("Get subscription details. See skills/azure/resourcemanagement/get-subscription.md only when using this tool")]
    public async Task<string> GetSubscription(string subscriptionId)
    {
        try
        {
            logger.LogDebug("Getting subscription {SubscriptionId}", subscriptionId);
            SubscriptionDto? subscription = await resourceService.GetSubscriptionAsync(subscriptionId);

            if (subscription is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Subscription {subscriptionId} not found"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                subscription
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting subscription {SubscriptionId}", subscriptionId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetSubscription",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_resource_groups")]
    [Description("List resource groups. See skills/azure/resourcemanagement/list-resource-groups.md only when using this tool")]
    public async Task<string> ListResourceGroups(string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing resource groups");
            IEnumerable<ResourceGroupDto> resourceGroups = await resourceService.GetResourceGroupsAsync(subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceGroups = resourceGroups.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing resource groups");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListResourceGroups",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_resource_group")]
    [Description("Get resource group details. See skills/azure/resourcemanagement/get-resource-group.md only when using this tool")]
    public async Task<string> GetResourceGroup(string subscriptionId, string resourceGroupName)
    {
        try
        {
            logger.LogDebug("Getting resource group {ResourceGroupName}", resourceGroupName);
            ResourceGroupDto? resourceGroup = await resourceService.GetResourceGroupAsync(subscriptionId, resourceGroupName);

            if (resourceGroup is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Resource group {resourceGroupName} not found"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceGroup
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting resource group {ResourceGroupName}", resourceGroupName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetResourceGroup",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_resources")]
    [Description("List Azure resources. See skills/azure/resourcemanagement/list-resources.md only when using this tool")]
    public async Task<string> ListResources(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing resources");
            List<GenericResourceDto> resources = (await resourceService.GetResourcesAsync(subscriptionId, resourceGroupName)).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                resources = resources.ToArray(),
                count = resources.Count
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing resources");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListResources",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_resources_by_type")]
    [Description("List resources by type. See skills/azure/resourcemanagement/list-resources-by-type.md only when using this tool")]
    public async Task<string> ListResourcesByType(string resourceType, string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing resources by type {ResourceType}", resourceType);
            List<GenericResourceDto> resources = (await resourceService.GetResourcesByTypeAsync(resourceType, subscriptionId)).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceType,
                resources = resources.ToArray(),
                count = resources.Count
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing resources by type {ResourceType}", resourceType);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListResourcesByType",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_resource")]
    [Description("Get resource by ID. See skills/azure/resourcemanagement/get-resource.md only when using this tool")]
    public async Task<string> GetResource(string resourceId)
    {
        try
        {
            logger.LogDebug("Getting resource {ResourceId}", resourceId);
            GenericResourceDto? resource = await resourceService.GetResourceAsync(resourceId);

            if (resource is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Resource {resourceId} not found"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                resource
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting resource {ResourceId}", resourceId);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetResource",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_resource_count_by_type")]
    [Description("Get resource count by type. See skills/azure/resourcemanagement/get-resource-count-by-type.md only when using this tool")]
    public async Task<string> GetResourceCountByType(string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting resource count by type");
            Dictionary<string, int> countByType = await resourceService.GetResourceCountByTypeAsync(subscriptionId);

            var sortedCounts = countByType
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new { resourceType = kvp.Key, count = kvp.Value })
                .ToArray();

            return JsonSerializer.Serialize(new
            {
                success = true,
                totalTypes = sortedCounts.Length,
                resourceCounts = sortedCounts
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting resource count by type");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetResourceCountByType",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }
}
