using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AzureServer.Core.Services.Core;
using AzureServer.Core.Services.Networking;
using AzureServer.Core.Services.Networking.Interfaces;

namespace AzureServer.Core.Configuration;

public static class Networking
{
    public static IServiceCollection AddNetworkingServices(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        services.AddScoped<IApplicationGatewayService>(provider =>
        {
            var logger = provider.GetService<ILogger<ApplicationGatewayService>>() ??
                         loggerFactory.CreateLogger<ApplicationGatewayService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ApplicationGatewayService(armClientFactory, logger);
        });
        
        services.AddScoped<IExpressRouteService>(provider =>
        {
            var logger = provider.GetService<ILogger<ExpressRouteService>>() ??
                         loggerFactory.CreateLogger<ExpressRouteService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ExpressRouteService(armClientFactory, logger);
        });
        
        services.AddScoped<ILoadBalancerService>(provider =>
        {
            var logger = provider.GetService<ILogger<LoadBalancerService>>() ??
                         loggerFactory.CreateLogger<LoadBalancerService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new LoadBalancerService(armClientFactory, logger);
        });
        
        services.AddScoped<INetworkInterfaceService>(provider =>
        {
            var logger = provider.GetService<ILogger<NetworkInterfaceService>>() ??
                         loggerFactory.CreateLogger<NetworkInterfaceService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new NetworkInterfaceService(armClientFactory, logger);
        });
        
        services.AddScoped<IPrivateEndpointService>(provider =>
        {
            var logger = provider.GetService<ILogger<PrivateEndpointService>>() ??
                         loggerFactory.CreateLogger<PrivateEndpointService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new PrivateEndpointService(armClientFactory, logger);
        });
        
        services.AddScoped<IPublicIpAddressService>(provider =>
        {
            var logger = provider.GetService<ILogger<PublicIpAddressService>>() ??
                         loggerFactory.CreateLogger<PublicIpAddressService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new PublicIpAddressService(armClientFactory, logger);
        });
        
        services.AddScoped<ISecurityRuleService>(provider =>
        {
            var logger = provider.GetService<ILogger<SecurityRuleService>>() ??
                         loggerFactory.CreateLogger<SecurityRuleService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SecurityRuleService(armClientFactory, logger);
        });
        
        services.AddScoped<ISubnetService>(provider =>
        {
            var logger = provider.GetService<ILogger<SubnetService>>() ??
                         loggerFactory.CreateLogger<SubnetService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SubnetService(armClientFactory, logger);
        });
        
        services.AddScoped<IVirtualNetworkService>(provider =>
        {
            var logger = provider.GetService<ILogger<VirtualNetworkService>>() ??
                         loggerFactory.CreateLogger<VirtualNetworkService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new VirtualNetworkService(armClientFactory, logger);
        });
        
        services.AddScoped<IVpnGatewayService>(provider =>
        {
            var logger = provider.GetService<ILogger<VpnGatewayService>>() ??
                         loggerFactory.CreateLogger<VpnGatewayService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new VpnGatewayService(armClientFactory, logger);
        });
        
        services.AddScoped<INetworkSecurityGroupService>(provider =>
        {
            var logger = provider.GetService<ILogger<NetworkSecurityGroupService>>() ??
                         loggerFactory.CreateLogger<NetworkSecurityGroupService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new NetworkSecurityGroupService(armClientFactory, logger);
        });
        
        services.AddScoped<INetworkWatcherService>(provider =>
        {
            var logger = provider.GetService<ILogger<NetworkWatcherService>>() ??
                         loggerFactory.CreateLogger<NetworkWatcherService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new NetworkWatcherService(armClientFactory, logger);
        });

        
        return services;
    }
}