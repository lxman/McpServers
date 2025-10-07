using AzureMcp.Services.Core;
using AzureMcp.Services.Networking;
using AzureMcp.Services.Networking.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Configuration;

public static class Networking
{
    public static IServiceCollection AddNetworkingServices(this IServiceCollection services, ILoggerFactory loggerFactory)
    {
        services.AddScoped<IApplicationGatewayService>(provider =>
        {
            ILogger<ApplicationGatewayService> logger =
                loggerFactory.CreateLogger<ApplicationGatewayService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ApplicationGatewayService(armClientFactory, logger);
        });
        
        services.AddScoped<IExpressRouteService>(provider =>
        {
            ILogger<ExpressRouteService> logger =
                loggerFactory.CreateLogger<ExpressRouteService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ExpressRouteService(armClientFactory, logger);
        });
        
        services.AddScoped<ILoadBalancerService>(provider =>
        {
            ILogger<LoadBalancerService> logger =
                loggerFactory.CreateLogger<LoadBalancerService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new LoadBalancerService(armClientFactory, logger);
        });
        
        services.AddScoped<INetworkInterfaceService>(provider =>
        {
            ILogger<NetworkInterfaceService> logger =
                loggerFactory.CreateLogger<NetworkInterfaceService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new NetworkInterfaceService(armClientFactory, logger);
        });
        
        services.AddScoped<IPrivateEndpointService>(provider =>
        {
            ILogger<PrivateEndpointService> logger =
                loggerFactory.CreateLogger<PrivateEndpointService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new PrivateEndpointService(armClientFactory, logger);
        });
        
        services.AddScoped<IPublicIpAddressService>(provider =>
        {
            ILogger<PublicIpAddressService> logger =
                loggerFactory.CreateLogger<PublicIpAddressService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new PublicIpAddressService(armClientFactory, logger);
        });
        
        services.AddScoped<ISecurityRuleService>(provider =>
        {
            ILogger<SecurityRuleService> logger =
                loggerFactory.CreateLogger<SecurityRuleService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SecurityRuleService(armClientFactory, logger);
        });
        
        services.AddScoped<ISubnetService>(provider =>
        {
            ILogger<SubnetService> logger =
                loggerFactory.CreateLogger<SubnetService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SubnetService(armClientFactory, logger);
        });
        
        services.AddScoped<IVirtualNetworkService>(provider =>
        {
            ILogger<VirtualNetworkService> logger =
                loggerFactory.CreateLogger<VirtualNetworkService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new VirtualNetworkService(armClientFactory, logger);
        });
        
        services.AddScoped<IVpnGatewayService>(provider =>
        {
            ILogger<VpnGatewayService> logger =
                loggerFactory.CreateLogger<VpnGatewayService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new VpnGatewayService(armClientFactory, logger);
        });
        
        return services;
    }
}