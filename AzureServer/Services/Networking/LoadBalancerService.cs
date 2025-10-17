using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Services.Core;
using AzureServer.Services.Networking.Interfaces;
using AzureServer.Services.Networking.Models;
using Microsoft.VisualStudio.Services.Common;

namespace AzureServer.Services.Networking;

public class LoadBalancerService(ArmClientFactory armClientFactory, ILogger<LoadBalancerService> logger) : ILoadBalancerService
{
    public async Task<IEnumerable<LoadBalancerDto>> ListLoadBalancersAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var loadBalancers = new List<LoadBalancerDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    var resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (var loadBalancer in resourceGroup.GetLoadBalancers())
                    {
                        loadBalancers.Add(MappingService.MapToLoadBalancerDto(loadBalancer.Data));
                    }

                    break;
                }
                case false:
                {
                    var subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (var loadBalancer in subscription.GetLoadBalancersAsync())
                    {
                        loadBalancers.Add(MappingService.MapToLoadBalancerDto(loadBalancer.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (var subscription in armClient.GetSubscriptions())
                    {
                        await foreach (var loadBalancer in subscription.GetLoadBalancersAsync())
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
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = LoadBalancerResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, loadBalancerName);
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
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceGroup = armClient.GetResourceGroupResource(
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
                foreach (var tag in request.Tags)
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
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = LoadBalancerResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, loadBalancerName);
            var loadBalancer = armClient.GetLoadBalancerResource(resourceId);
            
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
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = LoadBalancerResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, loadBalancerName);
            var loadBalancer = armClient.GetLoadBalancerResource(resourceId);
            
            Response<LoadBalancerResource>? response = await loadBalancer.GetAsync();
            var lbData = response.Value.Data;

            if (request.Tags is not null)
            {
                lbData.Tags.Clear();
                foreach (var tag in request.Tags)
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