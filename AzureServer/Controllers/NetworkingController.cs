using AzureServer.Services.Networking.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AzureServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NetworkingController(
    IVirtualNetworkService virtualNetworkService,
    ISubnetService subnetService,
    INetworkSecurityGroupService networkSecurityGroupService,
    IPublicIpAddressService publicIpAddressService,
    ILoadBalancerService loadBalancerService,
    ILogger<NetworkingController> logger) : ControllerBase
{
    [HttpGet("vnets")]
    public async Task<ActionResult> ListVirtualNetworks(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var vnets = await virtualNetworkService.ListVirtualNetworksAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, virtualNetworks = vnets.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing virtual networks");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListVirtualNetworks", type = ex.GetType().Name });
        }
    }

    [HttpGet("vnets/{resourceGroupName}/{vnetName}")]
    public async Task<ActionResult> GetVirtualNetwork(
        string resourceGroupName,
        string vnetName,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var vnet = await virtualNetworkService.GetVirtualNetworkAsync(resourceGroupName, vnetName, subscriptionId);
            if (vnet is null)
                return NotFound(new { success = false, error = $"Virtual network {vnetName} not found" });

            return Ok(new { success = true, virtualNetwork = vnet });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting virtual network {VNetName}", vnetName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetVirtualNetwork", type = ex.GetType().Name });
        }
    }

    [HttpGet("vnets/{resourceGroupName}/{vnetName}/subnets")]
    public async Task<ActionResult> ListSubnets(
        string resourceGroupName,
        string vnetName,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var subnets = await subnetService.ListSubnetsAsync(resourceGroupName, vnetName, subscriptionId);
            return Ok(new { success = true, subnets = subnets.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing subnets in VNet {VNetName}", vnetName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListSubnets", type = ex.GetType().Name });
        }
    }

    [HttpGet("nsg")]
    public async Task<ActionResult> ListNetworkSecurityGroups(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var nsgs = await networkSecurityGroupService.ListNetworkSecurityGroupsAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, networkSecurityGroups = nsgs.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing network security groups");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListNetworkSecurityGroups", type = ex.GetType().Name });
        }
    }

    [HttpGet("nsg/{resourceGroupName}/{nsgName}")]
    public async Task<ActionResult> GetNetworkSecurityGroup(
        string resourceGroupName,
        string nsgName,
        [FromQuery] string? subscriptionId = null)
    {
        try
        {
            var nsg = await networkSecurityGroupService.GetNetworkSecurityGroupAsync(resourceGroupName, nsgName, subscriptionId);
            if (nsg is null)
                return NotFound(new { success = false, error = $"Network security group {nsgName} not found" });

            return Ok(new { success = true, networkSecurityGroup = nsg });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting network security group {NsgName}", nsgName);
            return StatusCode(500, new { success = false, error = ex.Message, operation = "GetNetworkSecurityGroup", type = ex.GetType().Name });
        }
    }

    [HttpGet("public-ips")]
    public async Task<ActionResult> ListPublicIpAddresses(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var publicIps = await publicIpAddressService.ListPublicIpAddressesAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, publicIpAddresses = publicIps.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing public IP addresses");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListPublicIpAddresses", type = ex.GetType().Name });
        }
    }

    [HttpGet("load-balancers")]
    public async Task<ActionResult> ListLoadBalancers(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroupName = null)
    {
        try
        {
            var loadBalancers = await loadBalancerService.ListLoadBalancersAsync(subscriptionId, resourceGroupName);
            return Ok(new { success = true, loadBalancers = loadBalancers.ToArray() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing load balancers");
            return StatusCode(500, new { success = false, error = ex.Message, operation = "ListLoadBalancers", type = ex.GetType().Name });
        }
    }
}