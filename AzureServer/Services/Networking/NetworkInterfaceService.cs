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

public class NetworkInterfaceService(ArmClientFactory armClientFactory, ILogger<NetworkInterfaceService> logger) : INetworkInterfaceService
{
    public async Task<IEnumerable<NetworkInterfaceDto>> ListNetworkInterfacesAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var nics = new List<NetworkInterfaceDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (NetworkInterfaceResource? nic in resourceGroup.GetNetworkInterfaces())
                    {
                        nics.Add(MappingService.MapToNetworkInterfaceDto(nic.Data));
                    }

                    break;
                }
                case false:
                {
                    SubscriptionResource subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (NetworkInterfaceResource? nic in subscription.GetNetworkInterfacesAsync())
                    {
                        nics.Add(MappingService.MapToNetworkInterfaceDto(nic.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                    {
                        await foreach (NetworkInterfaceResource? nic in subscription.GetNetworkInterfacesAsync())
                        {
                            nics.Add(MappingService.MapToNetworkInterfaceDto(nic.Data));
                        }
                    }

                    break;
                }
            }

            return nics;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing network interfaces");
            throw;
        }
    }

    public async Task<NetworkInterfaceDto?> GetNetworkInterfaceAsync(string subscriptionId, string resourceGroupName, string nicName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = NetworkInterfaceResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nicName);
            Response<NetworkInterfaceResource>? response = await armClient.GetNetworkInterfaceResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToNetworkInterfaceDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting network interface {NicName}", nicName);
            throw;
        }
    }

    public async Task<NetworkInterfaceDto> CreateNetworkInterfaceAsync(string subscriptionId, string resourceGroupName, NetworkInterfaceCreateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var nicData = new NetworkInterfaceData
            {
                Location = request.Location,
                EnableAcceleratedNetworking = request.EnableAcceleratedNetworking,
                EnableIPForwarding = request.EnableIPForwarding
            };

            var ipConfig = new NetworkInterfaceIPConfigurationData
            {
                Name = "primary",
                Subnet = new SubnetData { Id = new ResourceIdentifier(request.SubnetId) },
                Primary = true
            };

            if (!string.IsNullOrEmpty(request.PrivateIPAddress))
            {
                ipConfig.PrivateIPAddress = request.PrivateIPAddress;
                ipConfig.PrivateIPAllocationMethod = NetworkIPAllocationMethod.Static;
            }
            else
            {
                ipConfig.PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic;
            }

            if (!string.IsNullOrEmpty(request.PublicIPAddressId))
                ipConfig.PublicIPAddress = new PublicIPAddressData { Id = new ResourceIdentifier(request.PublicIPAddressId) };

            nicData.IPConfigurations.Add(ipConfig);

            if (!string.IsNullOrEmpty(request.NetworkSecurityGroupId))
                nicData.NetworkSecurityGroup = new NetworkSecurityGroupData { Id = new ResourceIdentifier(request.NetworkSecurityGroupId) };

            if (request.Tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                    nicData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<NetworkInterfaceResource>? operation = await resourceGroup.GetNetworkInterfaces().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, nicData);
            
            return MappingService.MapToNetworkInterfaceDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating network interface {NicName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteNetworkInterfaceAsync(string subscriptionId, string resourceGroupName, string nicName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = NetworkInterfaceResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nicName);
            NetworkInterfaceResource? nic = armClient.GetNetworkInterfaceResource(resourceId);
            
            await nic.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting network interface {NicName}", nicName);
            throw;
        }
    }
}