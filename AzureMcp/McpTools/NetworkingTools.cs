using System.ComponentModel;
using System.Text.Json;
using AzureServer.Core.Services.Networking.Interfaces;
using AzureServer.Core.Services.Networking.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace AzureMcp.McpTools;

/// <summary>
/// MCP tools for Azure Networking operations
/// </summary>
[McpServerToolType]
public class NetworkingTools(
    IVirtualNetworkService virtualNetworkService,
    ISubnetService subnetService,
    INetworkSecurityGroupService networkSecurityGroupService,
    IPublicIpAddressService publicIpAddressService,
    ILoadBalancerService loadBalancerService,
    ILogger<NetworkingTools> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    [McpServerTool, DisplayName("list_virtual_networks")]
    [Description("List virtual networks. See skills/azure/networking/list-virtual-networks.md only when using this tool")]
    public async Task<string> ListVirtualNetworks(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing virtual networks");
            IEnumerable<VirtualNetworkDto> vnets = await virtualNetworkService.ListVirtualNetworksAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                virtualNetworks = vnets.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing virtual networks");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListVirtualNetworks",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_virtual_network")]
    [Description("Get virtual network details. See skills/azure/networking/get-virtual-network.md only when using this tool")]
    public async Task<string> GetVirtualNetwork(
        string resourceGroupName,
        string vnetName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting virtual network {VNetName}", vnetName);
            VirtualNetworkDto? vnet = await virtualNetworkService.GetVirtualNetworkAsync(resourceGroupName, vnetName, subscriptionId);

            if (vnet is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Virtual network {vnetName} not found"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                virtualNetwork = vnet
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting virtual network {VNetName}", vnetName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetVirtualNetwork",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_subnets")]
    [Description("List subnets in virtual network. See skills/azure/networking/list-subnets.md only when using this tool")]
    public async Task<string> ListSubnets(
        string resourceGroupName,
        string vnetName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Listing subnets in VNet {VNetName}", vnetName);
            IEnumerable<SubnetDto> subnets = await subnetService.ListSubnetsAsync(resourceGroupName, vnetName, subscriptionId);

            return JsonSerializer.Serialize(new
            {
                success = true,
                subnets = subnets.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing subnets in VNet {VNetName}", vnetName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListSubnets",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_network_security_groups")]
    [Description("List network security groups. See skills/azure/networking/list-network-security-groups.md only when using this tool")]
    public async Task<string> ListNetworkSecurityGroups(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing network security groups");
            IEnumerable<NetworkSecurityGroupDto> nsgs = await networkSecurityGroupService.ListNetworkSecurityGroupsAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                networkSecurityGroups = nsgs.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing network security groups");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListNetworkSecurityGroups",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("get_network_security_group")]
    [Description("Get network security group details. See skills/azure/networking/get-network-security-group.md only when using this tool")]
    public async Task<string> GetNetworkSecurityGroup(
        string resourceGroupName,
        string nsgName,
        string? subscriptionId = null)
    {
        try
        {
            logger.LogDebug("Getting network security group {NsgName}", nsgName);
            NetworkSecurityGroupDto? nsg = await networkSecurityGroupService.GetNetworkSecurityGroupAsync(resourceGroupName, nsgName, subscriptionId);

            if (nsg is null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Network security group {nsgName} not found"
                }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                networkSecurityGroup = nsg
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting network security group {NsgName}", nsgName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "GetNetworkSecurityGroup",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_public_ip_addresses")]
    [Description("List public IP addresses. See skills/azure/networking/list-public-ip-addresses.md only when using this tool")]
    public async Task<string> ListPublicIpAddresses(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing public IP addresses");
            IEnumerable<PublicIPAddressDto> publicIps = await publicIpAddressService.ListPublicIpAddressesAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                publicIpAddresses = publicIps.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing public IP addresses");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListPublicIpAddresses",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }

    [McpServerTool, DisplayName("list_load_balancers")]
    [Description("List load balancers. See skills/azure/networking/list-load-balancers.md only when using this tool")]
    public async Task<string> ListLoadBalancers(string? subscriptionId = null, string? resourceGroupName = null)
    {
        try
        {
            logger.LogDebug("Listing load balancers");
            IEnumerable<LoadBalancerDto> loadBalancers = await loadBalancerService.ListLoadBalancersAsync(subscriptionId, resourceGroupName);

            return JsonSerializer.Serialize(new
            {
                success = true,
                loadBalancers = loadBalancers.ToArray()
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing load balancers");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                operation = "ListLoadBalancers",
                type = ex.GetType().Name
            }, _jsonOptions);
        }
    }
}
