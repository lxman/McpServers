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

public class VirtualNetworkService(ArmClientFactory armClientFactory, ILogger<VirtualNetworkService> logger) : IVirtualNetworkService
{
    public async Task<IEnumerable<VirtualNetworkDto>> ListVirtualNetworksAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var vnets = new List<VirtualNetworkDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (VirtualNetworkResource? vnet in resourceGroup.GetVirtualNetworks())
                    {
                        vnets.Add(MappingService.MapToVirtualNetworkDto(vnet.Data));
                    }

                    break;
                }
                case false:
                {
                    SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (VirtualNetworkResource? vnet in subscription.GetVirtualNetworksAsync())
                    {
                        vnets.Add(MappingService.MapToVirtualNetworkDto(vnet.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                    {
                        await foreach (VirtualNetworkResource? vnet in subscription.GetVirtualNetworksAsync())
                        {
                            vnets.Add(MappingService.MapToVirtualNetworkDto(vnet.Data));
                        }
                    }

                    break;
                }
            }

            return vnets;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing virtual networks");
            throw;
        }
    }

    public async Task<VirtualNetworkDto?> GetVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = VirtualNetworkResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName);
            Response<VirtualNetworkResource>? response = await armClient.GetVirtualNetworkResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToVirtualNetworkDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting virtual network {VnetName}", vnetName);
            throw;
        }
    }

    public async Task<VirtualNetworkDto> CreateVirtualNetworkAsync(string subscriptionId, string resourceGroupName, VirtualNetworkCreateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var vnetData = new VirtualNetworkData
            {
                Location = request.Location,
                AddressSpace = new VirtualNetworkAddressSpace()
            };

            foreach (string prefix in request.AddressPrefixes)
            {
                vnetData.AddressSpace.AddressPrefixes.Add(prefix);
            }

            if (request.EnableDdosProtection.HasValue)
                vnetData.EnableDdosProtection = request.EnableDdosProtection.Value;

            if (request.EnableVmProtection.HasValue)
                vnetData.EnableVmProtection = request.EnableVmProtection.Value;

            if (request.Tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                    vnetData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<VirtualNetworkResource>? operation = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, vnetData);
            
            return MappingService.MapToVirtualNetworkDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating virtual network {VnetName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = VirtualNetworkResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName);
            VirtualNetworkResource? vnet = armClient.GetVirtualNetworkResource(resourceId);
            
            await vnet.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting virtual network {VnetName}", vnetName);
            throw;
        }
    }

    public async Task<VirtualNetworkDto> UpdateVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName, VirtualNetworkUpdateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = VirtualNetworkResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName);
            VirtualNetworkResource? vnet = armClient.GetVirtualNetworkResource(resourceId);
            
            Response<VirtualNetworkResource>? response = await vnet.GetAsync();
            VirtualNetworkData? vnetData = response.Value.Data;

            if (request.AddressPrefixes is not null)
            {
                vnetData.AddressSpace.AddressPrefixes.Clear();
                foreach (string prefix in request.AddressPrefixes)
                {
                    vnetData.AddressSpace.AddressPrefixes.Add(prefix);
                }
            }

            if (request.EnableDdosProtection.HasValue)
                vnetData.EnableDdosProtection = request.EnableDdosProtection.Value;

            if (request.EnableVmProtection.HasValue)
                vnetData.EnableVmProtection = request.EnableVmProtection.Value;

            if (request.Tags is not null)
            {
                vnetData.Tags.Clear();
                foreach (KeyValuePair<string, string> tag in request.Tags)
                    vnetData.Tags.Add(tag.Key, tag.Value);
            }

            var nto = new NetworkTagsObject();
            nto.Tags.AddRange(vnetData.Tags);
            Response<VirtualNetworkResource>? operation = await vnet.UpdateAsync(nto);
            return MappingService.MapToVirtualNetworkDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating virtual network {VnetName}", vnetName);
            throw;
        }
    }
}