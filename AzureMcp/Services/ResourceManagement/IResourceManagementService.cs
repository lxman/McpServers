using AzureMcp.Services.ResourceManagement.Models;

namespace AzureMcp.Services.ResourceManagement;

public interface IResourceManagementService
{
    Task<IEnumerable<SubscriptionDto>> GetSubscriptionsAsync();
    Task<SubscriptionDto?> GetSubscriptionAsync(string subscriptionId);
    Task<IEnumerable<ResourceGroupDto>> GetResourceGroupsAsync(string? subscriptionId = null);
    Task<ResourceGroupDto?> GetResourceGroupAsync(string subscriptionId, string resourceGroupName);
    Task<IEnumerable<GenericResourceDto>> GetResourcesAsync(string? subscriptionId = null, string? resourceGroupName = null);
    Task<IEnumerable<GenericResourceDto>> GetResourcesByTypeAsync(string resourceType, string? subscriptionId = null);
    Task<GenericResourceDto?> GetResourceAsync(string resourceId);
    Task<Dictionary<string, int>> GetResourceCountByTypeAsync(string? subscriptionId = null);
}
