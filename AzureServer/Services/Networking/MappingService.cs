using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using AzureServer.Services.Networking.Models;

namespace AzureServer.Services.Networking;

internal static class MappingService
{
    internal static VirtualNetworkDto MapToVirtualNetworkDto(VirtualNetworkData data)
    {
        return new VirtualNetworkDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            AddressPrefixes = data.AddressSpace?.AddressPrefixes?.ToList() ?? [],
            ProvisioningState = data.ProvisioningState?.ToString(),
            EnableDdosProtection = data.EnableDdosProtection,
            EnableVmProtection = data.EnableVmProtection,
            Subnets = data.Subnets?.Select(MapToSubnetDto).ToList() ?? [],
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static SubnetDto MapToSubnetDto(SubnetData data)
    {
        return new SubnetDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            AddressPrefix = data.AddressPrefix,
            ProvisioningState = data.ProvisioningState?.ToString(),
            NetworkSecurityGroupId = data.NetworkSecurityGroup?.Id?.ToString(),
            RouteTableId = data.RouteTable?.Id?.ToString(),
            ServiceEndpoints = data.ServiceEndpoints?.Select(e => e.Service).ToList() ?? [],
            PrivateEndpointNetworkPolicies = data.PrivateEndpointNetworkPolicy?.ToString() == "Enabled",
            PrivateLinkServiceNetworkPolicies = data.PrivateLinkServiceNetworkPolicy?.ToString() == "Enabled"
        };
    }

    internal static NetworkInterfaceDto MapToNetworkInterfaceDto(NetworkInterfaceData data)
    {
        return new NetworkInterfaceDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            MacAddress = data.MacAddress,
            EnableAcceleratedNetworking = data.EnableAcceleratedNetworking,
            EnableIPForwarding = data.EnableIPForwarding,
            IpConfigurations = data.IPConfigurations?.Select(MapToIpConfigurationDto).ToList() ?? [],
            NetworkSecurityGroupId = data.NetworkSecurityGroup?.Id?.ToString(),
            VirtualMachineId = data.VirtualMachineId?.ToString(),
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static IpConfigurationDto MapToIpConfigurationDto(NetworkInterfaceIPConfigurationData data)
    {
        return new IpConfigurationDto
        {
            Name = data.Name,
            PrivateIPAddress = data.PrivateIPAddress,
            PrivateIPAllocationMethod = data.PrivateIPAllocationMethod?.ToString(),
            PublicIPAddressId = data.PublicIPAddress?.Id?.ToString(),
            SubnetId = data.Subnet?.Id?.ToString(),
            Primary = data.Primary
        };
    }

    internal static NetworkSecurityGroupDto MapToNetworkSecurityGroupDto(NetworkSecurityGroupData data)
    {
        return new NetworkSecurityGroupDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            SecurityRules = data.SecurityRules?.Select(MapToSecurityRuleDto).ToList() ?? [],
            DefaultSecurityRules = data.DefaultSecurityRules?.Select(MapToSecurityRuleDto).ToList() ?? [],
            NetworkInterfaceIds = data.NetworkInterfaces?.Select(n => n.Id?.ToString()).Where(id => id is not null).ToList() ?? [],
            SubnetIds = data.Subnets?.Select(s => s.Id?.ToString()).Where(id => id is not null).ToList() ?? [],
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static SecurityRuleDto MapToSecurityRuleDto(SecurityRuleData data)
    {
        return new SecurityRuleDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Description = data.Description,
            Protocol = data.Protocol?.ToString(),
            SourcePortRange = data.SourcePortRange,
            DestinationPortRange = data.DestinationPortRange,
            SourceAddressPrefix = data.SourceAddressPrefix,
            DestinationAddressPrefix = data.DestinationAddressPrefix,
            Access = data.Access?.ToString(),
            Priority = data.Priority,
            Direction = data.Direction?.ToString(),
            ProvisioningState = data.ProvisioningState?.ToString()
        };
    }

    internal static SecurityRuleData MapToSecurityRuleData(SecurityRuleCreateRequest request)
    {
        return new SecurityRuleData
        {
            Name = request.Name,
            Description = request.Description,
            Protocol = request.Protocol == "*" ? SecurityRuleProtocol.Asterisk : Enum.Parse<SecurityRuleProtocol>(request.Protocol),
            SourcePortRange = request.SourcePortRange,
            DestinationPortRange = request.DestinationPortRange,
            SourceAddressPrefix = request.SourceAddressPrefix,
            DestinationAddressPrefix = request.DestinationAddressPrefix,
            Access = Enum.Parse<SecurityRuleAccess>(request.Access),
            Priority = request.Priority,
            Direction = Enum.Parse<SecurityRuleDirection>(request.Direction)
        };
    }

    internal static PublicIPAddressDto MapToPublicIpAddressDto(PublicIPAddressData data)
    {
        return new PublicIPAddressDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            IPAddress = data.IPAddress,
            PublicIPAllocationMethod = data.PublicIPAllocationMethod?.ToString(),
            PublicIPAddressVersion = data.PublicIPAddressVersion?.ToString(),
            IdleTimeoutInMinutes = data.IdleTimeoutInMinutes,
            DomainNameLabel = data.DnsSettings?.DomainNameLabel,
            Fqdn = data.DnsSettings?.Fqdn,
            AssociatedResourceId = data.IPConfiguration?.Id?.ToString(),
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static LoadBalancerDto MapToLoadBalancerDto(LoadBalancerData data)
    {
        return new LoadBalancerDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            Sku = data.Sku?.Name?.ToString(),
            FrontendIPConfigurations = data.FrontendIPConfigurations?.Select(fip => new FrontendIPConfigurationDto
            {
                Name = fip.Name,
                PrivateIPAddress = fip.PrivateIPAddress,
                PublicIPAddressId = fip.PublicIPAddress?.Id?.ToString(),
                SubnetId = fip.Subnet?.Id?.ToString()
            }).ToList() ?? [],
            BackendAddressPools = data.BackendAddressPools?.Select(bap => new BackendAddressPoolDto
            {
                Name = bap.Name,
                BackendIPConfigurations = bap.LoadBalancerBackendAddresses?.Select(addr => addr.NetworkInterfaceIPConfigurationId?.ToString()).Where(id => id is not null).ToList() ?? []
            }).ToList() ?? [],
            LoadBalancingRules = data.LoadBalancingRules?.Select(rule => new LoadBalancingRuleDto
            {
                Name = rule.Name,
                Protocol = rule.Protocol?.ToString(),
                FrontendPort = rule.FrontendPort,
                BackendPort = rule.BackendPort,
                EnableFloatingIP = rule.EnableFloatingIP,
                IdleTimeoutInMinutes = rule.IdleTimeoutInMinutes
            }).ToList() ?? [],
            Probes = data.Probes?.Select(probe => new ProbeDto
            {
                Name = probe.Name,
                Protocol = probe.Protocol?.ToString(),
                Port = probe.Port,
                RequestPath = probe.RequestPath,
                IntervalInSeconds = probe.IntervalInSeconds,
                NumberOfProbes = probe.NumberOfProbes
            }).ToList() ?? [],
            InboundNatRules = data.InboundNatRules?.Select(rule => new InboundNatRuleDto
            {
                Name = rule.Name,
                Protocol = rule.Protocol?.ToString(),
                FrontendPort = rule.FrontendPort,
                BackendPort = rule.BackendPort,
                EnableFloatingIP = rule.EnableFloatingIP
            }).ToList() ?? [],
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static VpnGatewayDto MapToVpnGatewayDto(VirtualNetworkGatewayData data)
    {
        return new VpnGatewayDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            GatewayType = data.GatewayType?.ToString(),
            VpnType = data.VpnType?.ToString(),
            Sku = data.Sku?.Name?.ToString(),
            EnableBgp = data.EnableBgp,
            // Note: ActiveActive configuration not directly available from VirtualNetworkGatewayData
            ActiveActive = null,
            // Note: Connections are separate resources and must be queried independently
            Connections = [],
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static ApplicationGatewayDto MapToApplicationGatewayDto(ApplicationGatewayData data)
    {
        return new ApplicationGatewayDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            Sku = data.Sku?.Name?.ToString(),
            OperationalState = data.OperationalState?.ToString(),
            Capacity = data.Sku?.Capacity,
            BackendPools = data.BackendAddressPools?.Select(bap => new BackendPoolDto
            {
                Name = bap.Name,
                // Map backend addresses to simple string list
                BackendAddresses = bap.BackendAddresses?.Select(addr => 
                    !string.IsNullOrEmpty(addr.Fqdn) ? addr.Fqdn : addr.IPAddress ?? string.Empty
                ).Where(addr => !string.IsNullOrEmpty(addr)).ToList() ?? []
            }).ToList() ?? [],
            HttpListeners = data.HttpListeners?.Select(listener => new HttpListenerDto
            {
                Name = listener.Name,
                Protocol = listener.Protocol?.ToString(),
                // Note: Frontend port is a reference, would need separate lookup for actual port number
                Port = null, // Frontend port number not directly available from reference
                HostName = listener.HostName
            }).ToList() ?? [],
            RequestRoutingRules = data.RequestRoutingRules?.Select(rule => new RequestRoutingRuleDto
            {
                Name = rule.Name,
                RuleType = rule.RuleType?.ToString(),
                // Map references to names (would need separate lookups for full resolution)
                // Note: HttpListener and BackendAddressPool are internal properties and cannot be accessed
                HttpListenerName = null,
                BackendPoolName = null
            }).ToList() ?? [],
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static PrivateEndpointDto MapToPrivateEndpointDto(PrivateEndpointData data)
    {
        return new PrivateEndpointDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            SubnetId = data.Subnet?.Id?.ToString(),
            PrivateLinkServiceConnectionState = data.PrivateLinkServiceConnections
                ?.FirstOrDefault()?.ConnectionState?.Status,
            NetworkInterfaceIds = data.NetworkInterfaces?.Select(n => n.Id?.ToString())
                .Where(id => id is not null)
                .ToList() ?? [],
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static ExpressRouteCircuitDto MapToExpressRouteCircuitDto(ExpressRouteCircuitData data)
    {
        return new ExpressRouteCircuitDto
        {
            Id = data.Id?.ToString(),
            Name = data.Name,
            Location = data.Location?.Name,
            ProvisioningState = data.ProvisioningState?.ToString(),
            CircuitProvisioningState = data.CircuitProvisioningState,
            ServiceProviderProvisioningState = data.ServiceProviderProvisioningState?.ToString(),
            ServiceProviderName = data.ServiceProviderProperties?.ServiceProviderName,
            PeeringLocation = data.ServiceProviderProperties?.PeeringLocation,
            BandwidthInMbps = data.ServiceProviderProperties?.BandwidthInMbps,
            SkuTier = data.Sku?.Tier?.ToString(),
            SkuFamily = data.Sku?.Family?.ToString(),
            AllowClassicOperations = data.AllowClassicOperations,
            ServiceKey = data.ServiceKey,
            Peerings = data.Peerings?.Select(MapToExpressRoutePeeringDto).ToList() ?? [],
            Tags = data.Tags?.ToDictionary(t => t.Key, t => t.Value) ?? new Dictionary<string, string>()
        };
    }

    internal static ExpressRoutePeeringDto MapToExpressRoutePeeringDto(ExpressRouteCircuitPeeringData data)
    {
        return new ExpressRoutePeeringDto
        {
            Name = data.Name,
            PeeringType = data.PeeringType?.ToString(),
            State = data.State?.ToString(),
            AzureASN = data.AzureASN,
            PeerASN = data.PeerASN != null ? (int)data.PeerASN : null,
            PrimaryPeerAddressPrefix = data.PrimaryPeerAddressPrefix,
            SecondaryPeerAddressPrefix = data.SecondaryPeerAddressPrefix,
            VlanId = data.VlanId
        };
    }
}