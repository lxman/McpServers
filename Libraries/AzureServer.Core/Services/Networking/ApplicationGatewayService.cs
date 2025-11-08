using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Networking.Interfaces;
using AzureServer.Core.Services.Networking.Models;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.Networking;

public class ApplicationGatewayService(ArmClientFactory armClientFactory, ILogger<ApplicationGatewayService> logger) : IApplicationGatewayService
{
    public async Task<IEnumerable<ApplicationGatewayDto>> ListApplicationGatewaysAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var appGateways = new List<ApplicationGatewayDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    var resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (var appGateway in resourceGroup.GetApplicationGateways())
                    {
                        appGateways.Add(MappingService.MapToApplicationGatewayDto(appGateway.Data));
                    }

                    break;
                }
                case false:
                {
                    var subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (var appGateway in subscription.GetApplicationGatewaysAsync())
                    {
                        appGateways.Add(MappingService.MapToApplicationGatewayDto(appGateway.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (var subscription in armClient.GetSubscriptions())
                    {
                        await foreach (var appGateway in subscription.GetApplicationGatewaysAsync())
                        {
                            appGateways.Add(MappingService.MapToApplicationGatewayDto(appGateway.Data));
                        }
                    }

                    break;
                }
            }

            return appGateways;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing application gateways");
            throw;
        }
    }

    public async Task<ApplicationGatewayDto?> GetApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = ApplicationGatewayResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, gatewayName);
            Response<ApplicationGatewayResource>? response = await armClient.GetApplicationGatewayResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToApplicationGatewayDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting application gateway {GatewayName}", gatewayName);
            throw;
        }
    }

    public async Task<ApplicationGatewayDto> CreateApplicationGatewayAsync(string subscriptionId, string resourceGroupName, ApplicationGatewayCreateRequest request)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var appGatewayData = new ApplicationGatewayData
            {
                Location = request.Location,
                Sku = new ApplicationGatewaySku
                {
                    Name = Enum.Parse<ApplicationGatewaySkuName>(request.Sku),
                    Tier = Enum.Parse<ApplicationGatewayTier>(request.Sku),
                    Capacity = request.Capacity
                }
            };

            if (request.Tags is not null)
            {
                foreach (var tag in request.Tags)
                    appGatewayData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<ApplicationGatewayResource>? operation = await resourceGroup.GetApplicationGateways().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, appGatewayData);
            
            return MappingService.MapToApplicationGatewayDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating application gateway {GatewayName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = ApplicationGatewayResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, gatewayName);
            var appGateway = armClient.GetApplicationGatewayResource(resourceId);
            
            await appGateway.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting application gateway {GatewayName}", gatewayName);
            throw;
        }
    }

    public async Task<ApplicationGatewayDto> StartApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = ApplicationGatewayResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, gatewayName);
            var appGateway = armClient.GetApplicationGatewayResource(resourceId);
            
            await appGateway.StartAsync(WaitUntil.Completed);
            
            Response<ApplicationGatewayResource>? response = await appGateway.GetAsync();
            return MappingService.MapToApplicationGatewayDto(response.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting application gateway {GatewayName}", gatewayName);
            throw;
        }
    }

    public async Task<ApplicationGatewayDto> StopApplicationGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = ApplicationGatewayResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, gatewayName);
            var appGateway = armClient.GetApplicationGatewayResource(resourceId);
            
            await appGateway.StopAsync(WaitUntil.Completed);
            
            Response<ApplicationGatewayResource>? response = await appGateway.GetAsync();
            return MappingService.MapToApplicationGatewayDto(response.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error stopping application gateway {GatewayName}", gatewayName);
            throw;
        }
    }
}
