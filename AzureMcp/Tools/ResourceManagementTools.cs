using AzureMcp.Services.ResourceManagement;
using AzureMcp.Services.ResourceManagement.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;

namespace AzureMcp.Tools;

[McpServerToolType]
public class ResourceManagementTools(IResourceManagementService resourceService)
{
    [McpServerTool]
    [Description("List all Azure subscriptions accessible to the authenticated user")]
    public async Task<string> ListSubscriptionsAsync()
    {
        try
        {
            IEnumerable<SubscriptionDto> subscriptions = await resourceService.GetSubscriptionsAsync();
            return JsonSerializer.Serialize(new { success = true, subscriptions = subscriptions.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListSubscriptions");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Azure subscription")]
    public async Task<string> GetSubscriptionAsync(
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SubscriptionDto? subscription = await resourceService.GetSubscriptionAsync(subscriptionId);
            if (subscription == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Subscription {subscriptionId} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, subscription },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetSubscription");
        }
    }

    [McpServerTool]
    [Description("List resource groups, optionally filtered by subscription")]
    public async Task<string> ListResourceGroupsAsync(
        [Description("Optional subscription ID to filter resource groups (if not provided, lists from all subscriptions)")]
        string? subscriptionId = null)
    {
        try
        {
            IEnumerable<ResourceGroupDto> resourceGroups = await resourceService.GetResourceGroupsAsync(subscriptionId);
            return JsonSerializer.Serialize(new { success = true, resourceGroups = resourceGroups.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListResourceGroups");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific resource group")]
    public async Task<string> GetResourceGroupAsync(
        [Description("Subscription ID")] string subscriptionId,
        [Description("Resource group name")] string resourceGroupName)
    {
        try
        {
            ResourceGroupDto? resourceGroup = await resourceService.GetResourceGroupAsync(subscriptionId, resourceGroupName);
            if (resourceGroup == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Resource group {resourceGroupName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, resourceGroup },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetResourceGroup");
        }
    }

    [McpServerTool]
    [Description("List all Azure resources, optionally filtered by subscription and/or resource group")]
    public async Task<string> ListResourcesAsync(
        [Description("Optional subscription ID to filter resources")] string? subscriptionId = null,
        [Description("Optional resource group name to filter resources")] string? resourceGroupName = null)
    {
        try
        {
            List<GenericResourceDto> resources = (await resourceService.GetResourcesAsync(subscriptionId, resourceGroupName)).ToList();
            return JsonSerializer.Serialize(new { success = true, resources = resources.ToArray(), count = resources.Count },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListResources");
        }
    }

    [McpServerTool]
    [Description("List Azure resources filtered by resource type (e.g., 'Microsoft.Storage/storageAccounts', 'Microsoft.Web/sites')")]
    public async Task<string> ListResourcesByTypeAsync(
        [Description("Resource type (e.g., 'Microsoft.Storage/storageAccounts', 'Microsoft.Compute/virtualMachines')")] string resourceType,
        [Description("Optional subscription ID to filter resources")] string? subscriptionId = null)
    {
        try
        {
            List<GenericResourceDto> resources = (await resourceService.GetResourcesByTypeAsync(resourceType, subscriptionId)).ToList();
            return JsonSerializer.Serialize(new { success = true, resourceType, resources = resources.ToArray(), count = resources.Count },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListResourcesByType");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific Azure resource by its resource ID")]
    public async Task<string> GetResourceAsync(
        [Description("Full resource ID (e.g., '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Storage/storageAccounts/{name}')")]
        string resourceId)
    {
        try
        {
            GenericResourceDto? resource = await resourceService.GetResourceAsync(resourceId);
            if (resource == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Resource {resourceId} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, resource },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetResource");
        }
    }

    [McpServerTool]
    [Description("Get a count of resources grouped by type, useful for understanding what resources exist in your subscription")]
    public async Task<string> GetResourceCountByTypeAsync(
        [Description("Optional subscription ID to filter resource count")] string? subscriptionId = null)
    {
        try
        {
            Dictionary<string, int> countByType = await resourceService.GetResourceCountByTypeAsync(subscriptionId);
            
            // Format for readability
            var sortedCounts = countByType
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => new { resourceType = kvp.Key, count = kvp.Value })
                .ToArray();

            return JsonSerializer.Serialize(new { success = true, totalTypes = sortedCounts.Length, resourceCounts = sortedCounts },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetResourceCountByType");
        }
    }

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = ex.Message,
            operation,
            type = ex.GetType().Name,
            stackTrace = ex.StackTrace
        }, SerializerOptions.JsonOptionsIndented);
    }
}
