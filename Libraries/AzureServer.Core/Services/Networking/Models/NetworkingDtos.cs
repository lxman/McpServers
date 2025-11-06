// ReSharper disable InconsistentNaming
namespace AzureServer.Core.Services.Networking.Models;

// Virtual Network DTOs
public class VirtualNetworkDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public List<string> AddressPrefixes { get; set; } = [];
    public string? ProvisioningState { get; set; }
    public bool? EnableDdosProtection { get; set; }
    public bool? EnableVmProtection { get; set; }
    public List<SubnetDto> Subnets { get; set; } = [];
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class VirtualNetworkCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<string> AddressPrefixes { get; set; } = [];
    public bool? EnableDdosProtection { get; set; }
    public bool? EnableVmProtection { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class VirtualNetworkUpdateRequest
{
    public List<string>? AddressPrefixes { get; set; }
    public bool? EnableDdosProtection { get; set; }
    public bool? EnableVmProtection { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// Subnet DTOs
public class SubnetDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? AddressPrefix { get; set; }
    public string? ProvisioningState { get; set; }
    public string? NetworkSecurityGroupId { get; set; }
    public string? RouteTableId { get; set; }
    public List<string> ServiceEndpoints { get; set; } = [];
    public bool? PrivateEndpointNetworkPolicies { get; set; }
    public bool? PrivateLinkServiceNetworkPolicies { get; set; }
}

public class SubnetCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string AddressPrefix { get; set; } = string.Empty;
    public string? NetworkSecurityGroupId { get; set; }
    public string? RouteTableId { get; set; }
    public List<string>? ServiceEndpoints { get; set; }
}

public class SubnetUpdateRequest
{
    public string? AddressPrefix { get; set; }
    public string? NetworkSecurityGroupId { get; set; }
    public string? RouteTableId { get; set; }
    public List<string>? ServiceEndpoints { get; set; }
}

// Network Interface DTOs
public class NetworkInterfaceDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public string? MacAddress { get; set; }
    public bool? EnableAcceleratedNetworking { get; set; }
    public bool? EnableIPForwarding { get; set; }
    public List<IpConfigurationDto> IpConfigurations { get; set; } = [];
    public string? NetworkSecurityGroupId { get; set; }
    public string? VirtualMachineId { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class IpConfigurationDto
{
    public string? Name { get; set; }
    public string? PrivateIPAddress { get; set; }
    public string? PrivateIPAllocationMethod { get; set; }
    public string? PublicIPAddressId { get; set; }
    public string? SubnetId { get; set; }
    public bool? Primary { get; set; }
}

public class NetworkInterfaceCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SubnetId { get; set; } = string.Empty;
    public string? PrivateIPAddress { get; set; }
    public string PrivateIPAllocationMethod { get; set; } = "Dynamic";
    public string? PublicIPAddressId { get; set; }
    public string? NetworkSecurityGroupId { get; set; }
    public bool? EnableAcceleratedNetworking { get; set; }
    public bool? EnableIPForwarding { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// Network Security Group DTOs
public class NetworkSecurityGroupDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public List<SecurityRuleDto> SecurityRules { get; set; } = [];
    public List<SecurityRuleDto> DefaultSecurityRules { get; set; } = [];
    public List<string?> NetworkInterfaceIds { get; set; } = [];
    public List<string?> SubnetIds { get; set; } = [];
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class NetworkSecurityGroupCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public List<SecurityRuleCreateRequest>? SecurityRules { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class NetworkSecurityGroupUpdateRequest
{
    public List<SecurityRuleCreateRequest>? SecurityRules { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// Security Rule DTOs
public class SecurityRuleDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Protocol { get; set; }
    public string? SourcePortRange { get; set; }
    public string? DestinationPortRange { get; set; }
    public string? SourceAddressPrefix { get; set; }
    public string? DestinationAddressPrefix { get; set; }
    public string? Access { get; set; }
    public int? Priority { get; set; }
    public string? Direction { get; set; }
    public string? ProvisioningState { get; set; }
}

public class SecurityRuleCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Protocol { get; set; } = "*";
    public string SourcePortRange { get; set; } = "*";
    public string DestinationPortRange { get; set; } = "*";
    public string SourceAddressPrefix { get; set; } = "*";
    public string DestinationAddressPrefix { get; set; } = "*";
    public string Access { get; set; } = "Allow";
    public int Priority { get; set; }
    public string Direction { get; set; } = "Inbound";
}

public class SecurityRuleUpdateRequest
{
    public string? Description { get; set; }
    public string? Protocol { get; set; }
    public string? SourcePortRange { get; set; }
    public string? DestinationPortRange { get; set; }
    public string? SourceAddressPrefix { get; set; }
    public string? DestinationAddressPrefix { get; set; }
    public string? Access { get; set; }
    public int? Priority { get; set; }
    public string? Direction { get; set; }
}

// Public IP Address DTOs
public class PublicIPAddressDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public string? IPAddress { get; set; }
    public string? PublicIPAllocationMethod { get; set; }
    public string? PublicIPAddressVersion { get; set; }
    public int? IdleTimeoutInMinutes { get; set; }
    public string? DomainNameLabel { get; set; }
    public string? Fqdn { get; set; }
    public string? AssociatedResourceId { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class PublicIPAddressCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string PublicIPAllocationMethod { get; set; } = "Static";
    public string PublicIPAddressVersion { get; set; } = "IPv4";
    public string Sku { get; set; } = "Standard";
    public int? IdleTimeoutInMinutes { get; set; }
    public string? DomainNameLabel { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// Load Balancer DTOs
public class LoadBalancerDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public string? Sku { get; set; }
    public List<FrontendIPConfigurationDto> FrontendIPConfigurations { get; set; } = [];
    public List<BackendAddressPoolDto> BackendAddressPools { get; set; } = [];
    public List<LoadBalancingRuleDto> LoadBalancingRules { get; set; } = [];
    public List<ProbeDto> Probes { get; set; } = [];
    public List<InboundNatRuleDto> InboundNatRules { get; set; } = [];
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class FrontendIPConfigurationDto
{
    public string? Name { get; set; }
    public string? PrivateIPAddress { get; set; }
    public string? PublicIPAddressId { get; set; }
    public string? SubnetId { get; set; }
}

public class BackendAddressPoolDto
{
    public string? Name { get; set; }
    public List<string?> BackendIPConfigurations { get; set; } = [];
}

public class LoadBalancingRuleDto
{
    public string? Name { get; set; }
    public string? Protocol { get; set; }
    public int? FrontendPort { get; set; }
    public int? BackendPort { get; set; }
    public bool? EnableFloatingIP { get; set; }
    public int? IdleTimeoutInMinutes { get; set; }
}

public class ProbeDto
{
    public string? Name { get; set; }
    public string? Protocol { get; set; }
    public int? Port { get; set; }
    public string? RequestPath { get; set; }
    public int? IntervalInSeconds { get; set; }
    public int? NumberOfProbes { get; set; }
}

public class InboundNatRuleDto
{
    public string? Name { get; set; }
    public string? Protocol { get; set; }
    public int? FrontendPort { get; set; }
    public int? BackendPort { get; set; }
    public bool? EnableFloatingIP { get; set; }
}

public class LoadBalancerCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Sku { get; set; } = "Standard";
    public List<FrontendIPConfigurationDto>? FrontendIPConfigurations { get; set; }
    public List<BackendAddressPoolDto>? BackendAddressPools { get; set; }
    public List<LoadBalancingRuleDto>? LoadBalancingRules { get; set; }
    public List<ProbeDto>? Probes { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

public class LoadBalancerUpdateRequest
{
    public List<LoadBalancingRuleDto>? LoadBalancingRules { get; set; }
    public List<ProbeDto>? Probes { get; set; }
    public List<InboundNatRuleDto>? InboundNatRules { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// VPN Gateway DTOs
public class VpnGatewayDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public string? GatewayType { get; set; }
    public string? VpnType { get; set; }
    public string? Sku { get; set; }
    public bool? EnableBgp { get; set; }
    public bool? ActiveActive { get; set; }
    public List<VpnConnectionDto> Connections { get; set; } = [];
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class VpnConnectionDto
{
    public string? Name { get; set; }
    public string? ConnectionStatus { get; set; }
    public string? ConnectionType { get; set; }
    public long? EgressBytesTransferred { get; set; }
    public long? IngressBytesTransferred { get; set; }
}

public class VpnGatewayCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string GatewayType { get; set; } = "Vpn";
    public string VpnType { get; set; } = "RouteBased";
    public string Sku { get; set; } = "VpnGw1";
    public string SubnetId { get; set; } = string.Empty;
    public string? PublicIPAddressId { get; set; }
    public bool? EnableBgp { get; set; }
    public bool? ActiveActive { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// Application Gateway DTOs
public class ApplicationGatewayDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public string? OperationalState { get; set; }
    public string? Sku { get; set; }
    public int? Capacity { get; set; }
    public List<BackendPoolDto> BackendPools { get; set; } = [];
    public List<HttpListenerDto> HttpListeners { get; set; } = [];
    public List<RequestRoutingRuleDto> RequestRoutingRules { get; set; } = [];
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class BackendPoolDto
{
    public string? Name { get; set; }
    public List<string> BackendAddresses { get; set; } = [];
}

public class HttpListenerDto
{
    public string? Name { get; set; }
    public string? Protocol { get; set; }
    public int? Port { get; set; }
    public string? HostName { get; set; }
}

public class RequestRoutingRuleDto
{
    public string? Name { get; set; }
    public string? RuleType { get; set; }
    public string? HttpListenerName { get; set; }
    public string? BackendPoolName { get; set; }
}

public class ApplicationGatewayCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Sku { get; set; } = "Standard_v2";
    public int Capacity { get; set; } = 2;
    public string SubnetId { get; set; } = string.Empty;
    public string? PublicIPAddressId { get; set; }
    public List<BackendPoolDto>? BackendPools { get; set; }
    public List<HttpListenerDto>? HttpListeners { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// Private Endpoint DTOs
public class PrivateEndpointDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public string? SubnetId { get; set; }
    public string? PrivateLinkServiceConnectionState { get; set; }
    public List<string?> NetworkInterfaceIds { get; set; } = [];
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class PrivateEndpointCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SubnetId { get; set; } = string.Empty;
    public string PrivateLinkServiceId { get; set; } = string.Empty;
    public List<string>? GroupIds { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}

// Network Watcher DTOs
public class NetworkWatcherDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class NetworkWatcherCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
}

public class ConnectivityCheckRequest
{
    public string SourceResourceId { get; set; } = string.Empty;
    public string? SourcePort { get; set; }
    public string DestinationResourceId { get; set; } = string.Empty;
    public string? DestinationAddress { get; set; }
    public int? DestinationPort { get; set; }
    public string Protocol { get; set; } = "Tcp";
}

public class ConnectivityCheckResult
{
    public string? ConnectionStatus { get; set; }
    public int? AvgLatencyInMs { get; set; }
    public int? MinLatencyInMs { get; set; }
    public int? MaxLatencyInMs { get; set; }
    public int? ProbesSent { get; set; }
    public int? ProbesFailed { get; set; }
    public List<HopDto> Hops { get; set; } = [];
}

public class HopDto
{
    public string? Type { get; set; }
    public string? Id { get; set; }
    public string? Address { get; set; }
    public List<string?> Issues { get; set; } = [];
}

public class NextHopRequest
{
    public string TargetVirtualMachineId { get; set; } = string.Empty;
    public string SourceIPAddress { get; set; } = string.Empty;
    public string DestinationIPAddress { get; set; } = string.Empty;
}

public class NextHopResult
{
    public string? NextHopType { get; set; }
    public string? NextHopIpAddress { get; set; }
    public string? RouteTableId { get; set; }
}

public class SecurityGroupViewResult
{
    public List<SecurityGroupNetworkInterface> NetworkInterfaces { get; set; } = [];
}

public class SecurityGroupNetworkInterface
{
    public string? NetworkInterfaceId { get; set; }
    public List<SecurityRuleAssociation> SecurityRuleAssociations { get; set; } = [];
}

public class SecurityRuleAssociation
{
    public string? Name { get; set; }
    public string? Direction { get; set; }
    public int? Priority { get; set; }
    public string? Access { get; set; }
    public string? SourceAddressPrefix { get; set; }
    public string? SourcePortRange { get; set; }
    public string? DestinationAddressPrefix { get; set; }
    public string? DestinationPortRange { get; set; }
}

// ExpressRoute DTOs
public class ExpressRouteCircuitDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? ProvisioningState { get; set; }
    public string? CircuitProvisioningState { get; set; }
    public string? ServiceProviderProvisioningState { get; set; }
    public string? ServiceProviderName { get; set; }
    public string? PeeringLocation { get; set; }
    public int? BandwidthInMbps { get; set; }
    public string? SkuTier { get; set; }
    public string? SkuFamily { get; set; }
    public bool? AllowClassicOperations { get; set; }
    public string? ServiceKey { get; set; }
    public List<ExpressRoutePeeringDto> Peerings { get; set; } = [];
    public Dictionary<string, string> Tags { get; set; } = new();
}

public class ExpressRoutePeeringDto
{
    public string? Name { get; set; }
    public string? PeeringType { get; set; }
    public string? State { get; set; }
    public int? AzureASN { get; set; }
    public int? PeerASN { get; set; }
    public string? PrimaryPeerAddressPrefix { get; set; }
    public string? SecondaryPeerAddressPrefix { get; set; }
    public int? VlanId { get; set; }
}

public class ExpressRouteCircuitCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ServiceProviderName { get; set; } = string.Empty;
    public string PeeringLocation { get; set; } = string.Empty;
    public int BandwidthInMbps { get; set; }
    public string SkuTier { get; set; } = "Standard";
    public string SkuFamily { get; set; } = "MeteredData";
    public bool? AllowClassicOperations { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
}
