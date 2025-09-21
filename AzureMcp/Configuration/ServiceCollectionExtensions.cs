using AzureMcp.Authentication;
using AzureMcp.Services.DevOps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<AzureOptions>(
            configuration.GetSection("Azure"));
        services.Configure<DevOpsOptions>(
            configuration.GetSection("AzureDevOps"));

        // Authentication - Azure AD/Resource Manager credentials
        services.AddSingleton<ICredentialManager, AzureCredentialManager>();
        
        // Authentication - Azure DevOps specific PAT-based credentials
        services.AddSingleton<DevOpsCredentialManager>(provider =>
        {
            DevOpsOptions devOpsOptions = configuration.GetSection("AzureDevOps").Get<DevOpsOptions>() 
                                          ?? new DevOpsOptions();
            
            if (string.IsNullOrEmpty(devOpsOptions.OrganizationUrl))
            {
                throw new InvalidOperationException("AzureDevOps:OrganizationUrl must be configured");
            }

            var logger = provider.GetRequiredService<ILogger<DevOpsCredentialManager>>();
            
            return new DevOpsCredentialManager(
                devOpsOptions.OrganizationUrl,
                configuration, 
                logger,
                devOpsOptions.CredentialTarget);
        });

        // Services
        services.AddScoped<IDevOpsService, DevOpsService>();

        // HTTP client configuration with resilience
        services.AddHttpClient();

        return services;
    }
}
