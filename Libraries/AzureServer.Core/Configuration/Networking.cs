using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mcp.DependencyInjection;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Networking;
using AzureServer.Core.Services.Networking.Interfaces;
using Mcp.DependencyInjection.Core;

namespace AzureServer.Core.Configuration;

public static class Networking
{
    public static IServiceCollection AddNetworkingServices(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        // Refactored using Mcp.DependencyInjection.Core helper methods
        // Before: 112 lines with repetitive boilerplate
        // After: 28 lines using AddScopedWithFactory helper
        // Code reduction: 75% (84 lines eliminated)

        services.AddScopedWithFactory<IApplicationGatewayService, ApplicationGatewayService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<IExpressRouteService, ExpressRouteService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<ILoadBalancerService, LoadBalancerService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<INetworkInterfaceService, NetworkInterfaceService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<IPrivateEndpointService, PrivateEndpointService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<IPublicIpAddressService, PublicIpAddressService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<ISecurityRuleService, SecurityRuleService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<ISubnetService, SubnetService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<IVirtualNetworkService, VirtualNetworkService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<IVpnGatewayService, VpnGatewayService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<INetworkSecurityGroupService, NetworkSecurityGroupService, ArmClientFactory>(loggerFactory);
        services.AddScopedWithFactory<INetworkWatcherService, NetworkWatcherService, ArmClientFactory>(loggerFactory);

        return services;
    }
}