using AzureMcp.Authentication;
using AzureMcp.Services.DevOps;
using AzureMcp.Services.DevOps.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Pure discovery-based Azure service configuration (no config files needed!)
    /// </summary>
    public static async Task<IServiceCollection> AddAzureServicesWithPureDiscoveryAsync(
        this IServiceCollection services)
    {
        // Create a temporary service provider for discovery
        var tempProvider = services.BuildServiceProvider();
        var loggerFactory = tempProvider.GetService<ILoggerFactory>() ?? 
                           LoggerFactory.Create(builder => builder.AddDebug());

        var discoveryLogger = loggerFactory.CreateLogger<AzureEnvironmentDiscovery>();
        var discovery = new AzureEnvironmentDiscovery(discoveryLogger);

        // Discover all Azure environments
        var discoveryResult = await discovery.DiscoverAzureEnvironmentsAsync();

        // Register discovery result for tools to access
        services.AddSingleton(discoveryResult);

        // Configure Azure Resource Manager credentials
        if (discoveryResult.AzureCredential != null)
        {
            services.AddSingleton<ICredentialManager>(provider =>
            {
                var logger = provider.GetService<ILogger<AzureCredentialManager>>() ??
                           loggerFactory.CreateLogger<AzureCredentialManager>();
                return new AzureCredentialManager(logger);
            });
        }

        // Configure Azure DevOps services
        if (discoveryResult.DevOpsEnvironments.Count > 0)
        {
            // Register DevOps credential manager for primary organization
            services.AddSingleton<DevOpsCredentialManager>(provider =>
            {
                var logger = provider.GetService<ILogger<DevOpsCredentialManager>>() ??
                           loggerFactory.CreateLogger<DevOpsCredentialManager>();
                var primaryEnv = discoveryResult.DevOpsEnvironments.First();
                return new DevOpsCredentialManager(primaryEnv, logger);
            });

            // Register multi-organization factory if multiple orgs found
            if (discoveryResult.DevOpsEnvironments.Count > 1)
            {
                services.AddSingleton<IMultiOrgDevOpsFactory>(provider =>
                {
                    var logger = provider.GetService<ILogger<MultiOrgDevOpsFactory>>() ??
                               loggerFactory.CreateLogger<MultiOrgDevOpsFactory>();
                    return new MultiOrgDevOpsFactory(discoveryResult.DevOpsEnvironments, logger);
                });
            }

            // Register DevOps service using existing constructor pattern
            services.AddScoped<IDevOpsService>(provider =>
            {
                var manager = provider.GetRequiredService<DevOpsCredentialManager>();
                var logger = provider.GetService<ILogger<DevOpsService>>() ??
                           loggerFactory.CreateLogger<DevOpsService>();
                return new DevOpsService(manager, logger);
            });
        }
        else
        {
            // Register a service that provides helpful guidance
            services.AddSingleton<IDevOpsService>(provider =>
            {
                var logger = provider.GetService<ILogger<DevOpsService>>() ??
                           loggerFactory.CreateLogger<DevOpsService>();
                return new NoCredentialsDevOpsService(logger);
            });
        }

        services.AddHttpClient();
        return services;
    }
}

/// <summary>
/// Multi-organization factory for discovered environments
/// </summary>
public interface IMultiOrgDevOpsFactory
{
    List<string> GetDiscoveredOrganizations();
    IDevOpsService CreateServiceForOrganization(string organizationUrl);
    DevOpsCredentialManager CreateManagerForOrganization(string organizationUrl);
}

public class MultiOrgDevOpsFactory : IMultiOrgDevOpsFactory
{
    private readonly List<DevOpsEnvironmentInfo> _environments;
    private readonly ILogger<MultiOrgDevOpsFactory> _logger;
    private readonly Dictionary<string, DevOpsCredentialManager> _managerCache = new();

    public MultiOrgDevOpsFactory(
        List<DevOpsEnvironmentInfo> environments,
        ILogger<MultiOrgDevOpsFactory> logger)
    {
        _environments = environments;
        _logger = logger;
    }

    public List<string> GetDiscoveredOrganizations()
    {
        return _environments.Select(e => e.OrganizationUrl).ToList();
    }

    public IDevOpsService CreateServiceForOrganization(string organizationUrl)
    {
        var manager = CreateManagerForOrganization(organizationUrl);
        var logger = LoggerFactory.Create(b => b.AddDebug()).CreateLogger<DevOpsService>();
        return new DevOpsService(manager, logger);
    }

    public DevOpsCredentialManager CreateManagerForOrganization(string organizationUrl)
    {
        if (_managerCache.TryGetValue(organizationUrl, out var cached))
            return cached;

        var environment = _environments.FirstOrDefault(e => 
            e.OrganizationUrl.Equals(organizationUrl, StringComparison.OrdinalIgnoreCase));
        
        if (environment == null)
        {
            var available = string.Join(", ", _environments.Select(e => e.OrganizationUrl));
            throw new InvalidOperationException(
                $"Organization '{organizationUrl}' not found in discovered environments. " +
                $"Available: {available}");
        }

        var logger = LoggerFactory.Create(b => b.AddDebug()).CreateLogger<DevOpsCredentialManager>();
        var manager = new DevOpsCredentialManager(environment, logger);

        _managerCache[organizationUrl] = manager;
        return manager;
    }
}

/// <summary>
/// Service that provides helpful guidance when no credentials are found
/// </summary>
public class NoCredentialsDevOpsService : IDevOpsService
{
    private readonly ILogger<DevOpsService> _logger;

    public NoCredentialsDevOpsService(ILogger<DevOpsService> logger)
    {
        _logger = logger;
    }

    // Implement IDevOpsService methods with correct return types
    public Task<IEnumerable<ProjectDto>> GetProjectsAsync()
    {
        throw new InvalidOperationException(
            "No Azure DevOps credentials discovered. To fix this:\n" +
            "1. Run: az login && az extension add --name azure-devops\n" +
            "2. Run: az devops configure --defaults organization=https://dev.azure.com/yourorg\n" +
            "3. OR store PAT in Windows Credential Manager (target: 'AzureDevOps')\n" +
            "4. OR set environment variables: AZURE_DEVOPS_PAT + AZURE_DEVOPS_ORG_URL");
    }

    public Task<ProjectDto?> GetProjectAsync(string projectName)
    {
        throw new InvalidOperationException(
            "No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<WorkItemDto?> GetWorkItemAsync(int id)
    {
        throw new InvalidOperationException(
            "No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<IEnumerable<WorkItemDto>> GetWorkItemsAsync(string projectName, string? wiql = null)
    {
        throw new InvalidOperationException(
            "No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<WorkItemDto> CreateWorkItemAsync(string projectName, string workItemType, string title, Dictionary<string, object>? fields = null)
    {
        throw new InvalidOperationException(
            "No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<IEnumerable<RepositoryDto>> GetRepositoriesAsync(string projectName)
    {
        throw new InvalidOperationException(
            "No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<RepositoryDto?> GetRepositoryAsync(string projectName, string repositoryName)
    {
        throw new InvalidOperationException(
            "No Azure DevOps credentials discovered. Please set up credentials first.");
    }
}