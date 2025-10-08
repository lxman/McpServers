using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using AzureMcp.Services.Core;
using AzureMcp.Services.Networking.Interfaces;
using AzureMcp.Services.Networking.Models;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Services.Networking;

public class PublicIpAddressService(ArmClientFactory armClientFactory, ILogger<PublicIpAddressService> logger) : IPublicIpAddressService
{
    public async Task<IEnumerable<PublicIPAddressDto>> ListPublicIpAddressesAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            var publicIps = new List<PublicIPAddressDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (PublicIPAddressResource? pip in resourceGroup.GetPublicIPAddresses())
                    {
                        publicIps.Add(MappingService.MapToPublicIpAddressDto(pip.Data));
                    }

                    break;
                }
                case false:
                {
                    SubscriptionResource? subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (PublicIPAddressResource? pip in subscription.GetPublicIPAddressesAsync())
                    {
                        publicIps.Add(MappingService.MapToPublicIpAddressDto(pip.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (SubscriptionResource? subscription in armClient.GetSubscriptions())
                    {
                        await foreach (PublicIPAddressResource? pip in subscription.GetPublicIPAddressesAsync())
                        {
                            publicIps.Add(MappingService.MapToPublicIpAddressDto(pip.Data));
                        }
                    }

                    break;
                }
            }

            return publicIps;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing public IP addresses");
            throw;
        }
    }

    public async Task<PublicIPAddressDto?> GetPublicIpAddressAsync(string subscriptionId, string resourceGroupName, string publicIpName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = PublicIPAddressResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, publicIpName);
            Response<PublicIPAddressResource>? response = await armClient.GetPublicIPAddressResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToPublicIpAddressDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting public IP address {PublicIpName}", publicIpName);
            throw;
        }
    }

    public async Task<PublicIPAddressDto> CreatePublicIpAddressAsync(string subscriptionId, string resourceGroupName, PublicIPAddressCreateRequest request)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceGroupResource? resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var pipData = new PublicIPAddressData
            {
                Location = request.Location,
                PublicIPAllocationMethod = request.PublicIPAllocationMethod == "Static" 
                    ? NetworkIPAllocationMethod.Static 
                    : NetworkIPAllocationMethod.Dynamic,
                PublicIPAddressVersion = request.PublicIPAddressVersion == "IPv6" 
                    ? NetworkIPVersion.IPv6 
                    : NetworkIPVersion.IPv4
            };

            pipData.Sku = new PublicIPAddressSku
            {
                Name = request.Sku == "Basic" ? PublicIPAddressSkuName.Basic : PublicIPAddressSkuName.Standard
            };

            if (request.IdleTimeoutInMinutes.HasValue)
                pipData.IdleTimeoutInMinutes = request.IdleTimeoutInMinutes.Value;

            if (!string.IsNullOrEmpty(request.DomainNameLabel))
            {
                pipData.DnsSettings = new PublicIPAddressDnsSettings
                {
                    DomainNameLabel = request.DomainNameLabel
                };
            }

            if (request.Tags is not null)
            {
                foreach (KeyValuePair<string, string> tag in request.Tags)
                    pipData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<PublicIPAddressResource>? operation = await resourceGroup.GetPublicIPAddresses().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, pipData);
            
            return MappingService.MapToPublicIpAddressDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating public IP address {PublicIpName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeletePublicIpAddressAsync(string subscriptionId, string resourceGroupName, string publicIpName)
    {
        try
        {
            ArmClient armClient = await armClientFactory.GetArmClientAsync();
            ResourceIdentifier? resourceId = PublicIPAddressResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, publicIpName);
            PublicIPAddressResource? pip = armClient.GetPublicIPAddressResource(resourceId);
            
            await pip.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting public IP address {PublicIpName}", publicIpName);
            throw;
        }
    }
}