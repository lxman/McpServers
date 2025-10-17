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

public class VpnGatewayService(ArmClientFactory armClientFactory, ILogger<VpnGatewayService> logger) : IVpnGatewayService
{
    public async Task<IEnumerable<VpnGatewayDto>> ListVpnGatewaysAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var gateways = new List<VpnGatewayDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    var resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (var gateway in resourceGroup.GetVirtualNetworkGateways())
                    {
                        gateways.Add(MappingService.MapToVpnGatewayDto(gateway.Data));
                    }

                    break;
                }
                case false:
                {
                    var subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (var resourceGroup in subscription.GetResourceGroups())
                    {
                        await foreach (var gateway in resourceGroup.GetVirtualNetworkGateways())
                        {
                            gateways.Add(MappingService.MapToVpnGatewayDto(gateway.Data));
                        }
                    }

                    break;
                }
                default:
                {
                    await foreach (var subscription in armClient.GetSubscriptions())
                    {
                        await foreach (var resourceGroup in subscription.GetResourceGroups())
                        {
                            await foreach (var gateway in resourceGroup.GetVirtualNetworkGateways())
                            {
                                gateways.Add(MappingService.MapToVpnGatewayDto(gateway.Data));
                            }
                        }
                    }

                    break;
                }
            }

            return gateways;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing VPN gateways");
            throw;
        }
    }

    public async Task<VpnGatewayDto?> GetVpnGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = VirtualNetworkGatewayResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, gatewayName);
            Response<VirtualNetworkGatewayResource>? response = await armClient.GetVirtualNetworkGatewayResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToVpnGatewayDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting VPN gateway {GatewayName}", gatewayName);
            throw;
        }
    }

    public async Task<VpnGatewayDto> CreateVpnGatewayAsync(string subscriptionId, string resourceGroupName, VpnGatewayCreateRequest request)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var gatewayData = new VirtualNetworkGatewayData
            {
                Location = request.Location,
                GatewayType = request.GatewayType == "Vpn" ? VirtualNetworkGatewayType.Vpn : VirtualNetworkGatewayType.ExpressRoute,
                VpnType = request.VpnType == "RouteBased" ? VpnType.RouteBased : VpnType.PolicyBased,
                EnableBgp = request.EnableBgp,
                Active = request.ActiveActive,
                Sku = new VirtualNetworkGatewaySku
                {
                    Name = Enum.Parse<VirtualNetworkGatewaySkuName>(request.Sku),
                    Tier = Enum.Parse<VirtualNetworkGatewaySkuTier>(request.Sku)
                }
            };

            var ipConfig = new VirtualNetworkGatewayIPConfiguration
            {
                Name = "default",
                PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                SubnetId = new ResourceIdentifier(request.SubnetId)
            };
            if (!string.IsNullOrEmpty(request.PublicIPAddressId))
                ipConfig.PublicIPAddressId = new ResourceIdentifier(request.PublicIPAddressId);

            gatewayData.IPConfigurations.Add(ipConfig);

            if (request.Tags is not null)
            {
                foreach (var tag in request.Tags)
                    gatewayData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<VirtualNetworkGatewayResource>? operation = await resourceGroup.GetVirtualNetworkGateways().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, gatewayData);
            
            return MappingService.MapToVpnGatewayDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating VPN gateway {GatewayName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteVpnGatewayAsync(string subscriptionId, string resourceGroupName, string gatewayName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = VirtualNetworkGatewayResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, gatewayName);
            var gateway = armClient.GetVirtualNetworkGatewayResource(resourceId);
            
            await gateway.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting VPN gateway {GatewayName}", gatewayName);
            throw;
        }
    }
}