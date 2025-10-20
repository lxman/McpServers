using AzureServer.Services.Core;
using AzureServer.Services.Networking;
using AzureServer.Services.Networking.Interfaces;

namespace AzureServer.Configuration;

public static class Networking
{
    public static IServiceCollection AddNetworkingServices(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        services.AddScoped<IApplicationGatewayService>(provider =>
        {
            ILogger<ApplicationGatewayService> logger = provider.GetService<ILogger<ApplicationGatewayService>>() ??
                                                        loggerFactory.CreateLogger<ApplicationGatewayService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ApplicationGatewayService(armClientFactory, logger);
        });
        
        services.AddScoped<IExpressRouteService>(provider =>
        {
            ILogger<ExpressRouteService> logger = provider.GetService<ILogger<ExpressRouteService>>() ??
                                                  loggerFactory.CreateLogger<ExpressRouteService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ExpressRouteService(armClientFactory, logger);
        });
        
        services.AddScoped<ILoadBalancerService>(provider =>
        {
            ILogger<LoadBalancerService> logger = provider.GetService<ILogger<LoadBalancerService>>() ??
                                                  loggerFactory.CreateLogger<LoadBalancerService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new LoadBalancerService(armClientFactory, logger);
        });
        
        services.AddScoped<INetworkInterfaceService>(provider =>
        {
            ILogger<NetworkInterfaceService> logger = provider.GetService<ILogger<NetworkInterfaceService>>() ??
                                                      loggerFactory.CreateLogger<NetworkInterfaceService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new NetworkInterfaceService(armClientFactory, logger);
        });
        
        services.AddScoped<IPrivateEndpointService>(provider =>
        {
            ILogger<PrivateEndpointService> logger = provider.GetService<ILogger<PrivateEndpointService>>() ??
                                                     loggerFactory.CreateLogger<PrivateEndpointService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new PrivateEndpointService(armClientFactory, logger);
        });
        
        services.AddScoped<IPublicIpAddressService>(provider =>
        {
            ILogger<PublicIpAddressService> logger = provider.GetService<ILogger<PublicIpAddressService>>() ??
                                                     loggerFactory.CreateLogger<PublicIpAddressService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new PublicIpAddressService(armClientFactory, logger);
        });
        
        services.AddScoped<ISecurityRuleService>(provider =>
        {
            ILogger<SecurityRuleService> logger = provider.GetService<ILogger<SecurityRuleService>>() ??
                                                  loggerFactory.CreateLogger<SecurityRuleService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SecurityRuleService(armClientFactory, logger);
        });
        
        services.AddScoped<ISubnetService>(provider =>
        {
            ILogger<SubnetService> logger = provider.GetService<ILogger<SubnetService>>() ??
                                            loggerFactory.CreateLogger<SubnetService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SubnetService(armClientFactory, logger);
        });
        
        services.AddScoped<IVirtualNetworkService>(provider =>
        {
            ILogger<VirtualNetworkService> logger = provider.GetService<ILogger<VirtualNetworkService>>() ??
                                                    loggerFactory.CreateLogger<VirtualNetworkService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new VirtualNetworkService(armClientFactory, logger);
        });
        
        services.AddScoped<IVpnGatewayService>(provider =>
        {
            ILogger<VpnGatewayService> logger = provider.GetService<ILogger<VpnGatewayService>>() ??
                                                loggerFactory.CreateLogger<VpnGatewayService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new VpnGatewayService(armClientFactory, logger);
        });
        
        services.AddScoped<INetworkSecurityGroupService>(provider =>
        {
            ILogger<NetworkSecurityGroupService> logger = provider.GetService<ILogger<NetworkSecurityGroupService>>() ??
                                                          loggerFactory.CreateLogger<NetworkSecurityGroupService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new NetworkSecurityGroupService(armClientFactory, logger);
        });
        
        services.AddScoped<INetworkWatcherService>(provider =>
        {
            ILogger<NetworkWatcherService> logger = provider.GetService<ILogger<NetworkWatcherService>>() ??
                                                    loggerFactory.CreateLogger<NetworkWatcherService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new NetworkWatcherService(armClientFactory, logger);
        });

        
        return services;
    }
}