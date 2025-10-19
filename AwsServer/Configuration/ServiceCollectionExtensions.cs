using Amazon.CloudWatchLogs;
using AwsServer.CloudWatch;
using AwsServer.ECR;
using AwsServer.ECS;
using AwsServer.QuickSight;
using AwsServer.S3;

namespace AwsServer.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsServices(this IServiceCollection services)
    {
        // Register AWS Discovery Service
        services.AddSingleton<AwsDiscoveryService>();
        
        // Register AWS service implementations
        services.AddAWSService<IAmazonCloudWatchLogs>();
        services.AddSingleton<S3Service>();
        services.AddSingleton<CloudWatchLogsService>(); // New simplified service
        services.AddSingleton<EcsService>();
        services.AddSingleton<EcrService>();
        services.AddSingleton<QuickSightService>();
        
        // Add response compression for large log responses
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });
        
        return services;
    }
}
