using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Networking.Interfaces;
using AzureServer.Core.Services.Networking.Models;
using Microsoft.VisualStudio.Services.Common;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.Networking;

public class LoadBalancerService(ArmClientFactory armClientFactory, ILogger<LoadBalancerService> logger) : ILoadBalancerService
{
    public async Task<IEnumerable<LoadBalancerDto>> ListLoadBalancersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var loadBalancers = new List<LoadBalancerDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (LoadBalancerResource? loadBalancer in resourceGroup.GetLoadBalancers())
                    {
                        loadBalancers.Add(MappingService.MapToLoadBalancerDto(loadBalancer.Data));
                    }

                    break;
                }
                case false:
                {
                    SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (LoadBalancerResource? loadBalancer in subscription.GetLoadBalancersAsync())
                    {
                        loadBalancers.Add(MappingService.MapToLoadBalancerDto(loadBalancer.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                    {
                        await foreach (LoadBalancerResource? loadBalancer in subscription.GetLoadBalancersAsync())
                        {
                            loadBalancers.Add(MappingService.MapToLoadBalancerDto(loadBalancer.Data));
                        }
                    }

                    break;
                }
            }

            return loadBalancers;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing load balancers");
            throw;
        }
    }

    public async Task<LoadBalancerDto?> GetLoadBalancerAsync(string subscriptionId, string resourceGroupName, string loadBalancerName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = LoadBalancerResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, loadBalancerName);
            Response<LoadBalancerResource>? response = await armClient.GetLoadBalancerResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToLoadBalancerDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting load balancer {LoadBalancerName}", loadBalancerName);
            throw;
        }
    }

    public async Task<LoadBalancerDto> CreateLoadBalancerAsync(string subscriptionId, string resourceGroupName, LoadBalancerCreateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var lbData = new LoadBalancerData
            {
                Location = request.Location,
                Sku = new LoadBalancerSku
                {
                    Name = request.Sku == "Basic" ? LoadBalancerSkuName.Basic : LoadBalancerSkuName.Standard
                }
            };

            if (request.Tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                    lbData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<LoadBalancerResource>? operation = await resourceGroup.GetLoadBalancers().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, lbData);
            
            return MappingService.MapToLoadBalancerDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating load balancer {LoadBalancerName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteLoadBalancerAsync(string subscriptionId, string resourceGroupName, string loadBalancerName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = LoadBalancerResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, loadBalancerName);
            LoadBalancerResource? loadBalancer = armClient.GetLoadBalancerResource(resourceId);
            
            await loadBalancer.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting load balancer {LoadBalancerName}", loadBalancerName);
            throw;
        }
    }

    public async Task<LoadBalancerDto> UpdateLoadBalancerAsync(string subscriptionId, string resourceGroupName, string loadBalancerName, LoadBalancerUpdateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = LoadBalancerResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, loadBalancerName);
            LoadBalancerResource? loadBalancer = armClient.GetLoadBalancerResource(resourceId);
            
            Response<LoadBalancerResource>? response = await loadBalancer.GetAsync();
            LoadBalancerData? lbData = response.Value.Data;

            if (request.Tags is not null)
            {
                lbData.Tags.Clear();
                foreach (KeyValuePair<string, string> tag in request.Tags)
                    lbData.Tags.Add(tag.Key, tag.Value);
            }

            var nto = new NetworkTagsObject();
            nto.Tags.AddRange(lbData.Tags);
            Response<LoadBalancerResource>? operation = await loadBalancer.UpdateAsync(nto);
            return MappingService.MapToLoadBalancerDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating load balancer {LoadBalancerName}", loadBalancerName);
            throw;
        }
    }
}
