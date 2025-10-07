using System.ComponentModel;
using System.Text.Json;
using AzureMcp.Common;
using AzureMcp.Services.Networking;
using AzureMcp.Services.Networking.Interfaces;
using AzureMcp.Services.Networking.Models;
using ModelContextProtocol.Server;

namespace AzureMcp.Tools;

[McpServerToolType]
public class NetworkingTools(
    IVirtualNetworkService virtualNetworkService,
    ISubnetService subnetService,
    INetworkSecurityGroupService networkSecurityGroupService,
    ISecurityRuleService securityRuleService,
    IApplicationGatewayService applicationGatewayService,
    ILoadBalancerService loadBalancerService,
    IPublicIpAddressService publicIpAddressService,
    INetworkInterfaceService networkInterfaceService,
    IPrivateEndpointService privateEndpointService,
    IExpressRouteService expressRouteService,
    IVpnGatewayService vpnGatewayService,
    INetworkWatcherService networkWatcherService)
{
    #region Virtual Network Operations

    [McpServerTool]
    [Description("List Azure virtual networks")]
    public async Task<string> ListVirtualNetworksAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<VirtualNetworkDto> vnets = await virtualNetworkService.ListVirtualNetworksAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, virtualNetworks = vnets.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListVirtualNetworks");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific virtual network")]
    public async Task<string> GetVirtualNetworkAsync(
        [Description("Virtual network name")] string vnetName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            VirtualNetworkDto? vnet = await virtualNetworkService.GetVirtualNetworkAsync(subscriptionId, resourceGroupName, vnetName);
            if (vnet is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Virtual network {vnetName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, virtualNetwork = vnet },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetVirtualNetwork");
        }
    }

    [McpServerTool]
    [Description("Create a new virtual network")]
    public async Task<string> CreateVirtualNetworkAsync(
        [Description("Virtual network creation request as JSON (name, location, addressPrefixes, tags, enableDdosProtection, enableVmProtection)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            VirtualNetworkCreateRequest? request = JsonSerializer.Deserialize<VirtualNetworkCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            VirtualNetworkDto vnet = await virtualNetworkService.CreateVirtualNetworkAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, virtualNetwork = vnet },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateVirtualNetwork");
        }
    }

    [McpServerTool]
    [Description("Delete a virtual network")]
    public async Task<string> DeleteVirtualNetworkAsync(
        [Description("Virtual network name")] string vnetName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await virtualNetworkService.DeleteVirtualNetworkAsync(subscriptionId, resourceGroupName, vnetName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Virtual network {vnetName} deleted" : $"Failed to delete virtual network {vnetName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteVirtualNetwork");
        }
    }

    [McpServerTool]
    [Description("Update a virtual network")]
    public async Task<string> UpdateVirtualNetworkAsync(
        [Description("Virtual network name")] string vnetName,
        [Description("Virtual network update request as JSON (addressPrefixes, tags, enableDdosProtection, enableVmProtection)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            VirtualNetworkUpdateRequest? request = JsonSerializer.Deserialize<VirtualNetworkUpdateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            VirtualNetworkDto vnet = await virtualNetworkService.UpdateVirtualNetworkAsync(subscriptionId, resourceGroupName, vnetName, request);
            return JsonSerializer.Serialize(new { success = true, virtualNetwork = vnet },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateVirtualNetwork");
        }
    }

    #endregion

    #region Subnet Operations

    [McpServerTool]
    [Description("List subnets in a virtual network")]
    public async Task<string> ListSubnetsAsync(
        [Description("Virtual network name")] string vnetName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            IEnumerable<SubnetDto> subnets = await subnetService.ListSubnetsAsync(subscriptionId, resourceGroupName, vnetName);
            return JsonSerializer.Serialize(new { success = true, subnets = subnets.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListSubnets");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific subnet")]
    public async Task<string> GetSubnetAsync(
        [Description("Subnet name")] string subnetName,
        [Description("Virtual network name")] string vnetName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SubnetDto? subnet = await subnetService.GetSubnetAsync(subscriptionId, resourceGroupName, vnetName, subnetName);
            if (subnet is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Subnet {subnetName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, subnet },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetSubnet");
        }
    }

    [McpServerTool]
    [Description("Create a new subnet in a virtual network")]
    public async Task<string> CreateSubnetAsync(
        [Description("Subnet creation request as JSON (name, addressPrefix, networkSecurityGroupId, routeTableId)")] string requestJson,
        [Description("Virtual network name")] string vnetName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SubnetCreateRequest? request = JsonSerializer.Deserialize<SubnetCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            SubnetDto subnet = await subnetService.CreateSubnetAsync(subscriptionId, resourceGroupName, vnetName, request);
            return JsonSerializer.Serialize(new { success = true, subnet },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateSubnet");
        }
    }

    [McpServerTool]
    [Description("Delete a subnet from a virtual network")]
    public async Task<string> DeleteSubnetAsync(
        [Description("Subnet name")] string subnetName,
        [Description("Virtual network name")] string vnetName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await subnetService.DeleteSubnetAsync(subscriptionId, resourceGroupName, vnetName, subnetName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Subnet {subnetName} deleted" : $"Failed to delete subnet {subnetName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteSubnet");
        }
    }

    [McpServerTool]
    [Description("Update a subnet")]
    public async Task<string> UpdateSubnetAsync(
        [Description("Subnet name")] string subnetName,
        [Description("Subnet update request as JSON (addressPrefix, networkSecurityGroupId, routeTableId)")] string requestJson,
        [Description("Virtual network name")] string vnetName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SubnetUpdateRequest? request = JsonSerializer.Deserialize<SubnetUpdateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            SubnetDto subnet = await subnetService.UpdateSubnetAsync(subscriptionId, resourceGroupName, vnetName, subnetName, request);
            return JsonSerializer.Serialize(new { success = true, subnet },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateSubnet");
        }
    }

    #endregion

    #region Network Security Group Operations

    [McpServerTool]
    [Description("List Azure network security groups")]
    public async Task<string> ListNetworkSecurityGroupsAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<NetworkSecurityGroupDto> nsgs = await networkSecurityGroupService.ListNetworkSecurityGroupsAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, networkSecurityGroups = nsgs.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListNetworkSecurityGroups");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific network security group")]
    public async Task<string> GetNetworkSecurityGroupAsync(
        [Description("Network security group name")] string nsgName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NetworkSecurityGroupDto? nsg = await networkSecurityGroupService.GetNetworkSecurityGroupAsync(subscriptionId, resourceGroupName, nsgName);
            if (nsg is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Network security group {nsgName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, networkSecurityGroup = nsg },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetNetworkSecurityGroup");
        }
    }

    [McpServerTool]
    [Description("Create a new network security group")]
    public async Task<string> CreateNetworkSecurityGroupAsync(
        [Description("Network security group creation request as JSON (name, location, securityRules, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NetworkSecurityGroupCreateRequest? request = JsonSerializer.Deserialize<NetworkSecurityGroupCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            NetworkSecurityGroupDto nsg = await networkSecurityGroupService.CreateNetworkSecurityGroupAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, networkSecurityGroup = nsg },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateNetworkSecurityGroup");
        }
    }

    [McpServerTool]
    [Description("Delete a network security group")]
    public async Task<string> DeleteNetworkSecurityGroupAsync(
        [Description("Network security group name")] string nsgName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await networkSecurityGroupService.DeleteNetworkSecurityGroupAsync(subscriptionId, resourceGroupName, nsgName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Network security group {nsgName} deleted" : $"Failed to delete network security group {nsgName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteNetworkSecurityGroup");
        }
    }

    [McpServerTool]
    [Description("Update a network security group")]
    public async Task<string> UpdateNetworkSecurityGroupAsync(
        [Description("Network security group name")] string nsgName,
        [Description("Network security group update request as JSON (securityRules, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NetworkSecurityGroupUpdateRequest? request = JsonSerializer.Deserialize<NetworkSecurityGroupUpdateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            NetworkSecurityGroupDto nsg = await networkSecurityGroupService.UpdateNetworkSecurityGroupAsync(subscriptionId, resourceGroupName, nsgName, request);
            return JsonSerializer.Serialize(new { success = true, networkSecurityGroup = nsg },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateNetworkSecurityGroup");
        }
    }

    #endregion

    #region Security Rule Operations

    [McpServerTool]
    [Description("List security rules in a network security group")]
    public async Task<string> ListSecurityRulesAsync(
        [Description("Network security group name")] string nsgName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            IEnumerable<SecurityRuleDto> rules = await securityRuleService.ListSecurityRulesAsync(subscriptionId, resourceGroupName, nsgName);
            return JsonSerializer.Serialize(new { success = true, securityRules = rules.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListSecurityRules");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific security rule")]
    public async Task<string> GetSecurityRuleAsync(
        [Description("Security rule name")] string ruleName,
        [Description("Network security group name")] string nsgName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SecurityRuleDto? rule = await securityRuleService.GetSecurityRuleAsync(subscriptionId, resourceGroupName, nsgName, ruleName);
            if (rule is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Security rule {ruleName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, securityRule = rule },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetSecurityRule");
        }
    }

    [McpServerTool]
    [Description("Create a new security rule in a network security group")]
    public async Task<string> CreateSecurityRuleAsync(
        [Description("Security rule creation request as JSON (name, priority, direction, access, protocol, sourceAddressPrefix, sourcePortRange, destinationAddressPrefix, destinationPortRange)")] string requestJson,
        [Description("Network security group name")] string nsgName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SecurityRuleCreateRequest? request = JsonSerializer.Deserialize<SecurityRuleCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            SecurityRuleDto rule = await securityRuleService.CreateSecurityRuleAsync(subscriptionId, resourceGroupName, nsgName, request);
            return JsonSerializer.Serialize(new { success = true, securityRule = rule },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateSecurityRule");
        }
    }

    [McpServerTool]
    [Description("Delete a security rule from a network security group")]
    public async Task<string> DeleteSecurityRuleAsync(
        [Description("Security rule name")] string ruleName,
        [Description("Network security group name")] string nsgName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await securityRuleService.DeleteSecurityRuleAsync(subscriptionId, resourceGroupName, nsgName, ruleName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Security rule {ruleName} deleted" : $"Failed to delete security rule {ruleName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteSecurityRule");
        }
    }

    [McpServerTool]
    [Description("Update a security rule")]
    public async Task<string> UpdateSecurityRuleAsync(
        [Description("Security rule name")] string ruleName,
        [Description("Security rule update request as JSON (priority, direction, access, protocol, sourceAddressPrefix, sourcePortRange, destinationAddressPrefix, destinationPortRange)")] string requestJson,
        [Description("Network security group name")] string nsgName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SecurityRuleUpdateRequest? request = JsonSerializer.Deserialize<SecurityRuleUpdateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            SecurityRuleDto rule = await securityRuleService.UpdateSecurityRuleAsync(subscriptionId, resourceGroupName, nsgName, ruleName, request);
            return JsonSerializer.Serialize(new { success = true, securityRule = rule },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateSecurityRule");
        }
    }

    #endregion

    #region Application Gateway Operations

    [McpServerTool]
    [Description("List Azure application gateways")]
    public async Task<string> ListApplicationGatewaysAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ApplicationGatewayDto> appGateways = await applicationGatewayService.ListApplicationGatewaysAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, applicationGateways = appGateways.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListApplicationGateways");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific application gateway")]
    public async Task<string> GetApplicationGatewayAsync(
        [Description("Application gateway name")] string gatewayName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            ApplicationGatewayDto? gateway = await applicationGatewayService.GetApplicationGatewayAsync(subscriptionId, resourceGroupName, gatewayName);
            if (gateway is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Application gateway {gatewayName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, applicationGateway = gateway },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetApplicationGateway");
        }
    }

    [McpServerTool]
    [Description("Create a new application gateway")]
    public async Task<string> CreateApplicationGatewayAsync(
        [Description("Application gateway creation request as JSON (name, location, sku, capacity, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            ApplicationGatewayCreateRequest? request = JsonSerializer.Deserialize<ApplicationGatewayCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            ApplicationGatewayDto gateway = await applicationGatewayService.CreateApplicationGatewayAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, applicationGateway = gateway },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateApplicationGateway");
        }
    }

    [McpServerTool]
    [Description("Delete an application gateway")]
    public async Task<string> DeleteApplicationGatewayAsync(
        [Description("Application gateway name")] string gatewayName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await applicationGatewayService.DeleteApplicationGatewayAsync(subscriptionId, resourceGroupName, gatewayName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Application gateway {gatewayName} deleted" : $"Failed to delete application gateway {gatewayName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteApplicationGateway");
        }
    }

    [McpServerTool]
    [Description("Start an application gateway")]
    public async Task<string> StartApplicationGatewayAsync(
        [Description("Application gateway name")] string gatewayName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            ApplicationGatewayDto gateway = await applicationGatewayService.StartApplicationGatewayAsync(subscriptionId, resourceGroupName, gatewayName);
            return JsonSerializer.Serialize(new { success = true, applicationGateway = gateway, message = $"Application gateway {gatewayName} started" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StartApplicationGateway");
        }
    }

    [McpServerTool]
    [Description("Stop an application gateway")]
    public async Task<string> StopApplicationGatewayAsync(
        [Description("Application gateway name")] string gatewayName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            ApplicationGatewayDto gateway = await applicationGatewayService.StopApplicationGatewayAsync(subscriptionId, resourceGroupName, gatewayName);
            return JsonSerializer.Serialize(new { success = true, applicationGateway = gateway, message = $"Application gateway {gatewayName} stopped" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "StopApplicationGateway");
        }
    }

    #endregion

    #region Load Balancer Operations

    [McpServerTool]
    [Description("List Azure load balancers")]
    public async Task<string> ListLoadBalancersAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<LoadBalancerDto> loadBalancers = await loadBalancerService.ListLoadBalancersAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, loadBalancers = loadBalancers.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListLoadBalancers");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific load balancer")]
    public async Task<string> GetLoadBalancerAsync(
        [Description("Load balancer name")] string loadBalancerName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            LoadBalancerDto? loadBalancer = await loadBalancerService.GetLoadBalancerAsync(subscriptionId, resourceGroupName, loadBalancerName);
            if (loadBalancer is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Load balancer {loadBalancerName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, loadBalancer },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetLoadBalancer");
        }
    }

    [McpServerTool]
    [Description("Create a new load balancer")]
    public async Task<string> CreateLoadBalancerAsync(
        [Description("Load balancer creation request as JSON (name, location, sku, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            LoadBalancerCreateRequest? request = JsonSerializer.Deserialize<LoadBalancerCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            LoadBalancerDto loadBalancer = await loadBalancerService.CreateLoadBalancerAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, loadBalancer },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateLoadBalancer");
        }
    }

    [McpServerTool]
    [Description("Delete a load balancer")]
    public async Task<string> DeleteLoadBalancerAsync(
        [Description("Load balancer name")] string loadBalancerName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await loadBalancerService.DeleteLoadBalancerAsync(subscriptionId, resourceGroupName, loadBalancerName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Load balancer {loadBalancerName} deleted" : $"Failed to delete load balancer {loadBalancerName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteLoadBalancer");
        }
    }

    [McpServerTool]
    [Description("Update a load balancer")]
    public async Task<string> UpdateLoadBalancerAsync(
        [Description("Load balancer name")] string loadBalancerName,
        [Description("Load balancer update request as JSON (tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            LoadBalancerUpdateRequest? request = JsonSerializer.Deserialize<LoadBalancerUpdateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            LoadBalancerDto loadBalancer = await loadBalancerService.UpdateLoadBalancerAsync(subscriptionId, resourceGroupName, loadBalancerName, request);
            return JsonSerializer.Serialize(new { success = true, loadBalancer },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "UpdateLoadBalancer");
        }
    }

    #endregion

    #region Public IP Address Operations

    [McpServerTool]
    [Description("List Azure public IP addresses")]
    public async Task<string> ListPublicIpAddressesAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<PublicIPAddressDto> publicIps = await publicIpAddressService.ListPublicIpAddressesAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, publicIpAddresses = publicIps.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListPublicIpAddresses");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific public IP address")]
    public async Task<string> GetPublicIpAddressAsync(
        [Description("Public IP address name")] string publicIpName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            PublicIPAddressDto? publicIp = await publicIpAddressService.GetPublicIpAddressAsync(subscriptionId, resourceGroupName, publicIpName);
            if (publicIp is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Public IP address {publicIpName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, publicIpAddress = publicIp },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetPublicIpAddress");
        }
    }

    [McpServerTool]
    [Description("Create a new public IP address")]
    public async Task<string> CreatePublicIpAddressAsync(
        [Description("Public IP address creation request as JSON (name, location, allocationMethod, sku, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            PublicIPAddressCreateRequest? request = JsonSerializer.Deserialize<PublicIPAddressCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            PublicIPAddressDto publicIp = await publicIpAddressService.CreatePublicIpAddressAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, publicIpAddress = publicIp },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreatePublicIpAddress");
        }
    }

    [McpServerTool]
    [Description("Delete a public IP address")]
    public async Task<string> DeletePublicIpAddressAsync(
        [Description("Public IP address name")] string publicIpName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await publicIpAddressService.DeletePublicIpAddressAsync(subscriptionId, resourceGroupName, publicIpName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Public IP address {publicIpName} deleted" : $"Failed to delete public IP address {publicIpName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeletePublicIpAddress");
        }
    }

    #endregion

    #region Network Interface Operations

    [McpServerTool]
    [Description("List Azure network interfaces")]
    public async Task<string> ListNetworkInterfacesAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<NetworkInterfaceDto> nics = await networkInterfaceService.ListNetworkInterfacesAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, networkInterfaces = nics.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListNetworkInterfaces");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific network interface")]
    public async Task<string> GetNetworkInterfaceAsync(
        [Description("Network interface name")] string nicName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NetworkInterfaceDto? nic = await networkInterfaceService.GetNetworkInterfaceAsync(subscriptionId, resourceGroupName, nicName);
            if (nic is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Network interface {nicName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, networkInterface = nic },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetNetworkInterface");
        }
    }

    [McpServerTool]
    [Description("Create a new network interface")]
    public async Task<string> CreateNetworkInterfaceAsync(
        [Description("Network interface creation request as JSON (name, location, subnetId, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NetworkInterfaceCreateRequest? request = JsonSerializer.Deserialize<NetworkInterfaceCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            NetworkInterfaceDto nic = await networkInterfaceService.CreateNetworkInterfaceAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, networkInterface = nic },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateNetworkInterface");
        }
    }

    [McpServerTool]
    [Description("Delete a network interface")]
    public async Task<string> DeleteNetworkInterfaceAsync(
        [Description("Network interface name")] string nicName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await networkInterfaceService.DeleteNetworkInterfaceAsync(subscriptionId, resourceGroupName, nicName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Network interface {nicName} deleted" : $"Failed to delete network interface {nicName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteNetworkInterface");
        }
    }

    #endregion

    #region Private Endpoint Operations

    [McpServerTool]
    [Description("List Azure private endpoints")]
    public async Task<string> ListPrivateEndpointsAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<PrivateEndpointDto> privateEndpoints = await privateEndpointService.ListPrivateEndpointsAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, privateEndpoints = privateEndpoints.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListPrivateEndpoints");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific private endpoint")]
    public async Task<string> GetPrivateEndpointAsync(
        [Description("Private endpoint name")] string privateEndpointName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            PrivateEndpointDto? privateEndpoint = await privateEndpointService.GetPrivateEndpointAsync(subscriptionId, resourceGroupName, privateEndpointName);
            if (privateEndpoint is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Private endpoint {privateEndpointName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, privateEndpoint },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetPrivateEndpoint");
        }
    }

    [McpServerTool]
    [Description("Create a new private endpoint")]
    public async Task<string> CreatePrivateEndpointAsync(
        [Description("Private endpoint creation request as JSON (name, location, subnetId, privateLinkServiceId, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            PrivateEndpointCreateRequest? request = JsonSerializer.Deserialize<PrivateEndpointCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            PrivateEndpointDto privateEndpoint = await privateEndpointService.CreatePrivateEndpointAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, privateEndpoint },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreatePrivateEndpoint");
        }
    }

    [McpServerTool]
    [Description("Delete a private endpoint")]
    public async Task<string> DeletePrivateEndpointAsync(
        [Description("Private endpoint name")] string privateEndpointName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await privateEndpointService.DeletePrivateEndpointAsync(subscriptionId, resourceGroupName, privateEndpointName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Private endpoint {privateEndpointName} deleted" : $"Failed to delete private endpoint {privateEndpointName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeletePrivateEndpoint");
        }
    }

    #endregion

    #region ExpressRoute Operations

    [McpServerTool]
    [Description("List Azure ExpressRoute circuits")]
    public async Task<string> ListExpressRouteCircuitsAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<ExpressRouteCircuitDto> circuits = await expressRouteService.ListExpressRouteCircuitsAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, expressRouteCircuits = circuits.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListExpressRouteCircuits");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific ExpressRoute circuit")]
    public async Task<string> GetExpressRouteCircuitAsync(
        [Description("ExpressRoute circuit name")] string circuitName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            ExpressRouteCircuitDto? circuit = await expressRouteService.GetExpressRouteCircuitAsync(subscriptionId, resourceGroupName, circuitName);
            if (circuit is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"ExpressRoute circuit {circuitName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, expressRouteCircuit = circuit },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetExpressRouteCircuit");
        }
    }

    [McpServerTool]
    [Description("Create a new ExpressRoute circuit")]
    public async Task<string> CreateExpressRouteCircuitAsync(
        [Description("ExpressRoute circuit creation request as JSON (name, location, serviceProviderName, peeringLocation, bandwidthInMbps, sku, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            ExpressRouteCircuitCreateRequest? request = JsonSerializer.Deserialize<ExpressRouteCircuitCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            ExpressRouteCircuitDto circuit = await expressRouteService.CreateExpressRouteCircuitAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, expressRouteCircuit = circuit },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateExpressRouteCircuit");
        }
    }

    [McpServerTool]
    [Description("Delete an ExpressRoute circuit")]
    public async Task<string> DeleteExpressRouteCircuitAsync(
        [Description("ExpressRoute circuit name")] string circuitName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await expressRouteService.DeleteExpressRouteCircuitAsync(subscriptionId, resourceGroupName, circuitName);
            return JsonSerializer.Serialize(new { success, message = success ? $"ExpressRoute circuit {circuitName} deleted" : $"Failed to delete ExpressRoute circuit {circuitName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteExpressRouteCircuit");
        }
    }

    #endregion

    #region VPN Gateway Operations

    [McpServerTool]
    [Description("List Azure VPN gateways")]
    public async Task<string> ListVpnGatewaysAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<VpnGatewayDto> gateways = await vpnGatewayService.ListVpnGatewaysAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, vpnGateways = gateways.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListVpnGateways");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific VPN gateway")]
    public async Task<string> GetVpnGatewayAsync(
        [Description("VPN gateway name")] string gatewayName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            VpnGatewayDto? gateway = await vpnGatewayService.GetVpnGatewayAsync(subscriptionId, resourceGroupName, gatewayName);
            if (gateway is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"VPN gateway {gatewayName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, vpnGateway = gateway },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetVpnGateway");
        }
    }

    [McpServerTool]
    [Description("Create a new VPN gateway")]
    public async Task<string> CreateVpnGatewayAsync(
        [Description("VPN gateway creation request as JSON (name, location, gatewayType, vpnType, sku, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            VpnGatewayCreateRequest? request = JsonSerializer.Deserialize<VpnGatewayCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            VpnGatewayDto gateway = await vpnGatewayService.CreateVpnGatewayAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, vpnGateway = gateway },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateVpnGateway");
        }
    }

    [McpServerTool]
    [Description("Delete a VPN gateway")]
    public async Task<string> DeleteVpnGatewayAsync(
        [Description("VPN gateway name")] string gatewayName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await vpnGatewayService.DeleteVpnGatewayAsync(subscriptionId, resourceGroupName, gatewayName);
            return JsonSerializer.Serialize(new { success, message = success ? $"VPN gateway {gatewayName} deleted" : $"Failed to delete VPN gateway {gatewayName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteVpnGateway");
        }
    }

    #endregion

    #region Network Watcher Operations

    [McpServerTool]
    [Description("List Azure network watchers")]
    public async Task<string> ListNetworkWatchersAsync(
        [Description("Optional subscription ID")] string? subscriptionId = null,
        [Description("Optional resource group name")] string? resourceGroupName = null)
    {
        try
        {
            IEnumerable<NetworkWatcherDto> watchers = await networkWatcherService.ListNetworkWatchersAsync(subscriptionId, resourceGroupName);
            return JsonSerializer.Serialize(new { success = true, networkWatchers = watchers.ToArray() },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "ListNetworkWatchers");
        }
    }

    [McpServerTool]
    [Description("Get details of a specific network watcher")]
    public async Task<string> GetNetworkWatcherAsync(
        [Description("Network watcher name")] string watcherName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NetworkWatcherDto? watcher = await networkWatcherService.GetNetworkWatcherAsync(subscriptionId, resourceGroupName, watcherName);
            if (watcher is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = $"Network watcher {watcherName} not found" },
                    SerializerOptions.JsonOptionsIndented);
            }

            return JsonSerializer.Serialize(new { success = true, networkWatcher = watcher },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetNetworkWatcher");
        }
    }

    [McpServerTool]
    [Description("Create a new network watcher")]
    public async Task<string> CreateNetworkWatcherAsync(
        [Description("Network watcher creation request as JSON (name, location, tags)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NetworkWatcherCreateRequest? request = JsonSerializer.Deserialize<NetworkWatcherCreateRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            NetworkWatcherDto watcher = await networkWatcherService.CreateNetworkWatcherAsync(subscriptionId, resourceGroupName, request);
            return JsonSerializer.Serialize(new { success = true, networkWatcher = watcher },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CreateNetworkWatcher");
        }
    }

    [McpServerTool]
    [Description("Delete a network watcher")]
    public async Task<string> DeleteNetworkWatcherAsync(
        [Description("Network watcher name")] string watcherName,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            bool success = await networkWatcherService.DeleteNetworkWatcherAsync(subscriptionId, resourceGroupName, watcherName);
            return JsonSerializer.Serialize(new { success, message = success ? $"Network watcher {watcherName} deleted" : $"Failed to delete network watcher {watcherName}" },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "DeleteNetworkWatcher");
        }
    }

    [McpServerTool]
    [Description("Check connectivity between network resources")]
    public async Task<string> CheckConnectivityAsync(
        [Description("Network watcher name")] string watcherName,
        [Description("Connectivity check request as JSON (sourceVmId, destinationVmId, destinationAddress, destinationPort)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            ConnectivityCheckRequest? request = JsonSerializer.Deserialize<ConnectivityCheckRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            ConnectivityCheckResult result = await networkWatcherService.CheckConnectivityAsync(subscriptionId, resourceGroupName, watcherName, request);
            return JsonSerializer.Serialize(new { success = true, connectivityResult = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "CheckConnectivity");
        }
    }

    [McpServerTool]
    [Description("Get next hop information for network routing")]
    public async Task<string> GetNextHopAsync(
        [Description("Network watcher name")] string watcherName,
        [Description("Next hop request as JSON (sourceVmId, destinationIpAddress, sourceIpAddress)")] string requestJson,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            NextHopRequest? request = JsonSerializer.Deserialize<NextHopRequest>(requestJson);
            if (request is null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid request JSON" },
                    SerializerOptions.JsonOptionsIndented);
            }

            NextHopResult result = await networkWatcherService.GetNextHopAsync(subscriptionId, resourceGroupName, watcherName, request);
            return JsonSerializer.Serialize(new { success = true, nextHopResult = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetNextHop");
        }
    }

    [McpServerTool]
    [Description("Get security group view for a virtual machine")]
    public async Task<string> GetSecurityGroupViewAsync(
        [Description("Network watcher name")] string watcherName,
        [Description("Target VM resource ID")] string targetVmId,
        [Description("Resource group name")] string resourceGroupName,
        [Description("Subscription ID")] string subscriptionId)
    {
        try
        {
            SecurityGroupViewResult result = await networkWatcherService.GetSecurityGroupViewAsync(subscriptionId, resourceGroupName, watcherName, targetVmId);
            return JsonSerializer.Serialize(new { success = true, securityGroupView = result },
                SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            return HandleError(ex, "GetSecurityGroupView");
        }
    }

    #endregion

    #region Error Handling

    private static string HandleError(Exception ex, string operation)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            operation,
            error = ex.Message,
            type = ex.GetType().Name
        }, SerializerOptions.JsonOptionsIndented);
    }

    #endregion
}
