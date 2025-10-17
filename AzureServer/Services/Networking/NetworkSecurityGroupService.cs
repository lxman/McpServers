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

public class NetworkSecurityGroupService(ArmClientFactory armClientFactory, ILogger<NetworkSecurityGroupService> logger) : INetworkSecurityGroupService
{
    public async Task<IEnumerable<NetworkSecurityGroupDto>> ListNetworkSecurityGroupsAsync(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var nsgs = new List<NetworkSecurityGroupDto>();

            switch (string.IsNullOrEmpty(subscriptionId))
            {
                case false when !string.IsNullOrEmpty(resourceGroupName):
                {
                    var resourceGroup = armClient.GetResourceGroupResource(
                        ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
                
                    await foreach (var nsg in resourceGroup.GetNetworkSecurityGroups())
                    {
                        nsgs.Add(MappingService.MapToNetworkSecurityGroupDto(nsg.Data));
                    }

                    break;
                }
                case false:
                {
                    var subscription = armClient.GetSubscriptionResource(
                        new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
                
                    await foreach (var nsg in subscription.GetNetworkSecurityGroupsAsync())
                    {
                        nsgs.Add(MappingService.MapToNetworkSecurityGroupDto(nsg.Data));
                    }

                    break;
                }
                default:
                {
                    await foreach (var subscription in armClient.GetSubscriptions())
                    {
                        await foreach (var nsg in subscription.GetNetworkSecurityGroupsAsync())
                        {
                            nsgs.Add(MappingService.MapToNetworkSecurityGroupDto(nsg.Data));
                        }
                    }

                    break;
                }
            }

            return nsgs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing network security groups");
            throw;
        }
    }

    public async Task<NetworkSecurityGroupDto?> GetNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, string nsgName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = NetworkSecurityGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName);
            Response<NetworkSecurityGroupResource>? response = await armClient.GetNetworkSecurityGroupResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToNetworkSecurityGroupDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting network security group {NsgName}", nsgName);
            throw;
        }
    }

    public async Task<NetworkSecurityGroupDto> CreateNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, NetworkSecurityGroupCreateRequest request)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceGroup = armClient.GetResourceGroupResource(
                ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));

            var nsgData = new NetworkSecurityGroupData
            {
                Location = request.Location
            };

            if (request.SecurityRules is not null)
            {
                foreach (var rule in request.SecurityRules)
                {
                    nsgData.SecurityRules.Add(MappingService.MapToSecurityRuleData(rule));
                }
            }

            if (request.Tags is not null)
            {
                foreach (var tag in request.Tags)
                    nsgData.Tags.Add(tag.Key, tag.Value);
            }

            ArmOperation<NetworkSecurityGroupResource>? operation = await resourceGroup.GetNetworkSecurityGroups().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, nsgData);
            
            return MappingService.MapToNetworkSecurityGroupDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating network security group {NsgName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, string nsgName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = NetworkSecurityGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName);
            var nsg = armClient.GetNetworkSecurityGroupResource(resourceId);
            
            await nsg.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting network security group {NsgName}", nsgName);
            throw;
        }
    }

    public async Task<NetworkSecurityGroupDto> UpdateNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, string nsgName, NetworkSecurityGroupUpdateRequest request)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = NetworkSecurityGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, nsgName);
            var nsg = armClient.GetNetworkSecurityGroupResource(resourceId);
            
            Response<NetworkSecurityGroupResource>? response = await nsg.GetAsync();
            var nsgData = response.Value.Data;

            if (request.SecurityRules is not null)
            {
                nsgData.SecurityRules.Clear();
                foreach (var rule in request.SecurityRules)
                {
                    nsgData.SecurityRules.Add(MappingService.MapToSecurityRuleData(rule));
                }
            }

            if (request.Tags is not null)
            {
                nsgData.Tags.Clear();
                foreach (var tag in request.Tags)
                    nsgData.Tags.Add(tag.Key, tag.Value);
            }

            var nto = new NetworkTagsObject();
            nto.Tags.AddRange(nsgData.Tags);
            Response<NetworkSecurityGroupResource>? operation = await nsg.UpdateAsync(nto);
            return MappingService.MapToNetworkSecurityGroupDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating network security group {NsgName}", nsgName);
            throw;
        }
    }
}