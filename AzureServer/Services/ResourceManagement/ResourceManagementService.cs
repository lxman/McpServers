using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using AzureServer.Services.Core;
using AzureServer.Services.ResourceManagement.Models;

namespace AzureServer.Services.ResourceManagement;

public class ResourceManagementService(
    ArmClientFactory armClientFactory,
    ILogger<ResourceManagementService> logger)
    : IResourceManagementService
{
    public async Task<IEnumerable<SubscriptionDto>> GetSubscriptionsAsync()
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var subscriptions = new List<SubscriptionDto>();
            
            await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions().GetAllAsync())
            {
                subscriptions.Add(MapSubscription(subscription));
            }

            logger.LogInformation("Retrieved {Count} subscriptions", subscriptions.Count);
            return subscriptions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving subscriptions");
            throw;
        }
    }

    public async Task<SubscriptionDto?> GetSubscriptionAsync(string subscriptionId)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            SubscriptionResource subscription = await armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}")).GetAsync();
            
            return MapSubscription(subscription);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Subscription {SubscriptionId} not found", subscriptionId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task<IEnumerable<ResourceGroupDto>> GetResourceGroupsAsync(string? subscriptionId = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var resourceGroups = new List<ResourceGroupDto>();

            if (string.IsNullOrEmpty(subscriptionId))
            {
                // Get resource groups from all subscriptions
                await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions().GetAllAsync())
                {
                    await foreach (ResourceGroupResource? rg in subscription.GetResourceGroups().GetAllAsync())
                    {
                        resourceGroups.Add(MapResourceGroup(rg));
                    }
                }
            }
            else
            {
                // Get resource groups from specific subscription
                SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                await foreach (ResourceGroupResource? rg in subscription.GetResourceGroups().GetAllAsync())
                {
                    resourceGroups.Add(MapResourceGroup(rg));
                }
            }

            logger.LogInformation("Retrieved {Count} resource groups", resourceGroups.Count);
            return resourceGroups;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving resource groups");
            throw;
        }
    }

    public async Task<ResourceGroupDto?> GetResourceGroupAsync(string subscriptionId, string resourceGroupName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            
            ResourceGroupResource resourceGroup = await subscription.GetResourceGroups()
                .GetAsync(resourceGroupName);
            
            return MapResourceGroup(resourceGroup);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Resource group {ResourceGroup} not found in subscription {SubscriptionId}", 
                resourceGroupName, subscriptionId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving resource group {ResourceGroup}", resourceGroupName);
            throw;
        }
    }

    public async Task<IEnumerable<GenericResourceDto>> GetResourcesAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var resources = new List<GenericResourceDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    // Get resources from a specific resource group
                    SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                    ResourceGroupResource resourceGroup = await subscription.GetResourceGroups()
                        .GetAsync(resourceGroupName);

                    resources.AddRange(resourceGroup.GetGenericResources().Select(MapGenericResource));

                    break;
                }
                case false:
                {
                    // Get all resources from a specific subscription
                    SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                    resources.AddRange(subscription.GetGenericResources().Select(MapGenericResource));
                    break;
                }
                default:
                {
                    // Get all resources from all subscriptions
                    await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions().GetAllAsync())
                    {
                        resources.AddRange(subscription.GetGenericResources().Select(MapGenericResource));
                    }

                    break;
                }
            }

            logger.LogInformation("Retrieved {Count} resources", resources.Count);
            return resources;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving resources");
            throw;
        }
    }

    public async Task<IEnumerable<GenericResourceDto>> GetResourcesByTypeAsync(string resourceType, string? subscriptionId = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var resources = new List<GenericResourceDto>();

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                    new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

                resources.AddRange(subscription.GetGenericResources(filter: $"resourceType eq '{resourceType}'").Select(MapGenericResource));
            }
            else
            {
                await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions().GetAllAsync())
                {
                    resources.AddRange(subscription.GetGenericResources(filter: $"resourceType eq '{resourceType}'").Select(MapGenericResource));
                }
            }

            logger.LogInformation("Retrieved {Count} resources of type {Type}", resources.Count, resourceType);
            return resources;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving resources by type {Type}", resourceType);
            throw;
        }
    }

    public async Task<GenericResourceDto?> GetResourceAsync(string resourceId)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var id = new ResourceIdentifier(resourceId);
            GenericResource resource = await armClient.GetGenericResource(id).GetAsync();
            
            return MapGenericResource(resource);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogWarning("Resource {ResourceId} not found", resourceId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving resource {ResourceId}", resourceId);
            throw;
        }
    }

    public async Task<Dictionary<string, int>> GetResourceCountByTypeAsync(string? subscriptionId = null)
    {
        try
        {
            IEnumerable<GenericResourceDto> resources = await GetResourcesAsync(subscriptionId);
            Dictionary<string, int> countByType = resources
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            logger.LogInformation("Resource count by type: {Count} types found", countByType.Count);
            return countByType;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting resource count by type");
            throw;
        }
    }

    // Mapping methods
    private static SubscriptionDto MapSubscription(SubscriptionResource subscription)
    {
        return new SubscriptionDto
        {
            Id = subscription.Id.ToString(),
            SubscriptionId = subscription.Data.SubscriptionId,
            DisplayName = subscription.Data.DisplayName,
            State = subscription.Data.State.ToString() ?? string.Empty,
            TenantId = subscription.Data.TenantId?.ToString(),
            Tags = subscription.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    private static ResourceGroupDto MapResourceGroup(ResourceGroupResource resourceGroup)
    {
        return new ResourceGroupDto
        {
            Id = resourceGroup.Id.ToString(),
            Name = resourceGroup.Data.Name,
            Location = resourceGroup.Data.Location.Name,
            Type = resourceGroup.Data.ResourceType.ToString(),
            Tags = resourceGroup.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ManagedBy = resourceGroup.Data.ManagedBy,
            ProvisioningState = null // Not available in current SDK version
        };
    }

    private static GenericResourceDto MapGenericResource(GenericResource resource)
    {
        return new GenericResourceDto
        {
            Id = resource.Id.ToString(),
            Name = resource.Data.Name,
            Type = resource.Data.ResourceType.ToString(),
            Location = resource.Data.Location.Name,
            ResourceGroup = resource.Id.ResourceGroupName ?? string.Empty,
            Tags = resource.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Kind = resource.Data.Kind,
            Sku = resource.Data.Sku?.Name,
            ProvisioningState = resource.Data.ProvisioningState,
            CreatedTime = resource.Data.CreatedOn?.ToString("o"),
            ChangedTime = resource.Data.ChangedOn?.ToString("o")
        };
    }
}
