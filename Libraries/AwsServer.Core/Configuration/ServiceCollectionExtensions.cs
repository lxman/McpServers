using Microsoft.Extensions.DependencyInjection;
using Amazon.CloudWatchLogs;
using AwsServer.Core.Services.CloudWatch;
using AwsServer.Core.Services.ECR;
using AwsServer.Core.Services.ECS;
using AwsServer.Core.Services.QuickSight;
using AwsServer.Core.Services.S3;

namespace AwsServer.Core.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsServices(this IServiceCollection services)
    {
        // Register AWS Discovery Service
        services.AddSingleton<AwsDiscoveryService>();

        // Register AWS service implementations
        services.AddAWSService<IAmazonCloudWatchLogs>();
        services.AddSingleton<S3Service>();
        services.AddSingleton<CloudWatchLogsService>();
        services.AddSingleton<CloudWatchMetricsService>();  // New metrics service
        services.AddSingleton<EcsService>();
        services.AddSingleton<EcrService>();
        services.AddSingleton<QuickSightService>();

        return services;
    }
}