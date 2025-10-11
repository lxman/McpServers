using AwsServer.CloudWatch;
using AwsServer.ECR;
using AwsServer.ECS;
using AwsServer.QuickSight;
using AwsServer.S3;
using Microsoft.Extensions.DependencyInjection;

namespace AwsServer.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsServices(this IServiceCollection services)
    {
        // Register AWS Discovery Service
        services.AddSingleton<AwsDiscoveryService>();
        
        // Register AWS service implementations
        services.AddSingleton<S3Service>();
        services.AddSingleton<CloudWatchService>();
        services.AddSingleton<EcsService>();
        services.AddSingleton<EcrService>();
        services.AddSingleton<QuickSightService>();
        
        return services;
    }
}