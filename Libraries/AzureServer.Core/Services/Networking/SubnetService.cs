using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Networking.Interfaces;
using AzureServer.Core.Services.Networking.Models;

using Microsoft.Extensions.Logging;
namespace AzureServer.Core.Services.Networking;

public class SubnetService(ArmClientFactory armClientFactory, ILogger<SubnetService> logger) : ISubnetService
{
    public async Task<IEnumerable<SubnetDto>> ListSubnetsAsync(string subscriptionId, string resourceGroupName, string vnetName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = VirtualNetworkResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName);
            var vnet = armClient.GetVirtualNetworkResource(resourceId);
            
            var subnets = new List<SubnetDto>();
            await foreach (var subnet in vnet.GetSubnets())
            {
                subnets.Add(MappingService.MapToSubnetDto(subnet.Data));
            }
            
            return subnets;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing subnets for VNet {VnetName}", vnetName);
            throw;
        }
    }

    public async Task<SubnetDto?> GetSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, string subnetName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = SubnetResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName, subnetName);
            Response<SubnetResource>? response = await armClient.GetSubnetResource(resourceId).GetAsync();
            
            return response.HasValue ? MappingService.MapToSubnetDto(response.Value.Data) : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting subnet {SubnetName}", subnetName);
            throw;
        }
    }

    public async Task<SubnetDto> CreateSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, SubnetCreateRequest request)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var vnetResourceId = VirtualNetworkResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName);
            var vnet = armClient.GetVirtualNetworkResource(vnetResourceId);

            var subnetData = new SubnetData
            {
                AddressPrefix = request.AddressPrefix
            };

            if (!string.IsNullOrEmpty(request.NetworkSecurityGroupId))
                subnetData.NetworkSecurityGroup = new NetworkSecurityGroupData { Id = new ResourceIdentifier(request.NetworkSecurityGroupId) };

            if (!string.IsNullOrEmpty(request.RouteTableId))
                subnetData.RouteTable = new RouteTableData { Id = new ResourceIdentifier(request.RouteTableId) };

            if (request.ServiceEndpoints is not null)
            {
                foreach (var endpoint in request.ServiceEndpoints)
                {
                    subnetData.ServiceEndpoints.Add(new ServiceEndpointProperties { Service = endpoint });
                }
            }

            ArmOperation<SubnetResource>? operation = await vnet.GetSubnets().CreateOrUpdateAsync(
                WaitUntil.Completed, request.Name, subnetData);
            
            return MappingService.MapToSubnetDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating subnet {SubnetName}", request.Name);
            throw;
        }
    }

    public async Task<bool> DeleteSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, string subnetName)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = SubnetResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName, subnetName);
            var subnet = armClient.GetSubnetResource(resourceId);
            
            await subnet.DeleteAsync(WaitUntil.Completed);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting subnet {SubnetName}", subnetName);
            throw;
        }
    }

    public async Task<SubnetDto> UpdateSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, string subnetName, SubnetUpdateRequest request)
    {
        try
        {
            var armClient = await armClientFactory.GetArmClientAsync();
            var resourceId = SubnetResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, vnetName, subnetName);
            var subnet = armClient.GetSubnetResource(resourceId);
            
            Response<SubnetResource>? response = await subnet.GetAsync();
            var subnetData = response.Value.Data;

            if (!string.IsNullOrEmpty(request.AddressPrefix))
                subnetData.AddressPrefix = request.AddressPrefix;

            if (!string.IsNullOrEmpty(request.NetworkSecurityGroupId))
                subnetData.NetworkSecurityGroup = new NetworkSecurityGroupData { Id = new ResourceIdentifier(request.NetworkSecurityGroupId) };

            if (!string.IsNullOrEmpty(request.RouteTableId))
                subnetData.RouteTable = new RouteTableData { Id = new ResourceIdentifier(request.RouteTableId) };

            if (request.ServiceEndpoints is not null)
            {
                subnetData.ServiceEndpoints.Clear();
                foreach (var endpoint in request.ServiceEndpoints)
                {
                    subnetData.ServiceEndpoints.Add(new ServiceEndpointProperties { Service = endpoint });
                }
            }

            ArmOperation<SubnetResource>? operation = await subnet.UpdateAsync(WaitUntil.Completed, subnetData);
            return MappingService.MapToSubnetDto(operation.Value.Data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating subnet {SubnetName}", subnetName);
            throw;
        }
    }
}
