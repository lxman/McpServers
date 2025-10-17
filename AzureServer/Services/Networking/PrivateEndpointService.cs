using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Services.Core;
using AzureServer.Services.Networking.Interfaces;
using AzureServer.Services.Networking.Models;

namespace AzureServer.Services.Networking;

public class PrivateEndpointService(ArmClientFactory armClientFactory, ILogger<PrivateEndpointService> logger) : IPrivateEndpointService
{
    public async Task<IEnumerable<PrivateEndpointDto>> ListPrivateEndpointsAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var privateEndpoints = new List<PrivateEndpointDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    var resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (var privateEndpoint in resourceGroup.GetPrivateEndpoints())
                    {
                        privateEndpoints.Add(MappingService.MapToPrivateEndpointDto(privateEndpoint.Data));
                    }

                    break;
                }
                case false:
                {
                    var subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (var privateEndpoint in subscription.GetPrivateEndpointsAsync())
                    {
                        privateEndpoints.Add(MappingService.MapToPrivateEndpointDto(privateEndpoint.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (var subscription in armClient.GetSubscriptions())
                    {
                        await foreach (var privateEndpoint in subscription.GetPrivateEndpointsAsync())
                        {
                            privateEndpoints.Add(MappingService.MapToPrivateEndpointDto(privateEndpoint.Data));
                        }
                    }

                    break;
                }
            }

            return privateEndpoints;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing private endpoints");
            throw;
        }
    }

    public async Task<PrivateEndpointDto?> GetPrivateEndpointAsync(string subscriptionId, string resourceGroupName, string privateEndpointName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = PrivateEndpointResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, privateEndpointName);
            Response<PrivateEndpointResource>? response = await armClient.GetPrivateEndpointResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToPrivateEndpointDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting private endpoint {PrivateEndpointName}", privateEndpointName);
            throw;
        }
    }

    public async Task<PrivateEndpointDto> CreatePrivateEndpointAsync(string subscriptionId, string resourceGroupName, PrivateEndpointCreateRequest request)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var privateEndpointData = new PrivateEndpointData
            {
                Location = request.Location,
                Subnet = new SubnetData { Id = new ResourceIdentifier(request.SubnetId) }
            };

            if (!string.IsNullOrEmpty(request.PrivateLinkServiceId))
            {
                var connection = new NetworkPrivateLinkServiceConnection
                {
                    Name = $"{request.Name}-connection",
                    PrivateLinkServiceId = new ResourceIdentifier(request.PrivateLinkServiceId)
                };

                if (request.GroupIds is not null)
                {
                    foreach (var groupId in request.GroupIds)
                    {
                        connection.GroupIds.Add(groupId);
                    }
                }

                privateEndpointData.PrivateLinkServiceConnections.Add(connection);
            }

            if (request.Tags is not null)
            {
                foreach (var tag in request.Tags)
                    privateEndpointData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<PrivateEndpointResource>? operation = await resourceGroup.GetPrivateEndpoints().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, privateEndpointData);
            
            return MappingService.MapToPrivateEndpointDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating private endpoint {PrivateEndpointName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeletePrivateEndpointAsync(string subscriptionId, string resourceGroupName, string privateEndpointName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = PrivateEndpointResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, privateEndpointName);
            var privateEndpoint = armClient.GetPrivateEndpointResource(resourceId);
            
            await privateEndpoint.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting private endpoint {PrivateEndpointName}", privateEndpointName);
            throw;
        }
    }
}