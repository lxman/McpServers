using AzureMcp.Authentication;
using AzureMcp.Authentication.models;
using AzureMcp.Services.DevOps;
using AzureMcp.Services.DevOps.Models;
using AzureMcp.Services.ResourceManagement;
using AzureMcp.Services.CostManagement;
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
        ServiceProvider tempProvider = services.BuildServiceProvider();
        ILoggerFactory loggerFactory = tempProvider.GetService<ILoggerFactory>() ?? 
                                       LoggerFactory.Create(builder => builder.AddDebug());

        // Register credential discovery and selection services
        services.AddSingleton<CredentialDiscoveryService>(provider =>
        {
            ILogger<CredentialDiscoveryService> logger = provider.GetService<ILogger<CredentialDiscoveryService>>() ??
                            loggerFactory.CreateLogger<CredentialDiscoveryService>();
            return new CredentialDiscoveryService(logger);
        });

        services.AddSingleton<CredentialSelectionService>(provider =>
        {
            ILogger<CredentialSelectionService> logger = provider.GetService<ILogger<CredentialSelectionService>>() ??
                            loggerFactory.CreateLogger<CredentialSelectionService>();
            var discoveryService = provider.GetRequiredService<CredentialDiscoveryService>();
            return new CredentialSelectionService(logger, discoveryService);
        });


        // Discover Azure DevOps environments only (ARM credentials now handled by CredentialSelectionService)
        ILogger<AzureEnvironmentDiscovery> discoveryLogger = loggerFactory.CreateLogger<AzureEnvironmentDiscovery>();
        var discovery = new AzureEnvironmentDiscovery(discoveryLogger);
        List<DevOpsEnvironmentInfo> devOpsEnvironments = await discovery.DiscoverDevOpsEnvironmentsAsync();

        // Configure Azure Resource Management service using CredentialSelectionService
        services.AddScoped<IResourceManagementService>(provider =>
        {
            ILogger<ResourceManagementService> logger = provider.GetService<ILogger<ResourceManagementService>>() ??
                                                        loggerFactory.CreateLogger<ResourceManagementService>();
            var credentialService = provider.GetRequiredService<CredentialSelectionService>();
            return new ResourceManagementService(credentialService, logger);
        });

        // Configure Azure Cost Management service using CredentialSelectionService
        services.AddScoped<ICostManagementService>(provider =>
        {
            ILogger<CostManagementService> logger = provider.GetService<ILogger<CostManagementService>>() ??
                                                    loggerFactory.CreateLogger<CostManagementService>();
            var credentialService = provider.GetRequiredService<CredentialSelectionService>();
            return new CostManagementService(credentialService, logger);
        });


        // Configure Azure DevOps services
        if (devOpsEnvironments.Count > 0)
        {
            // Register DevOps credential manager for primary organization
            services.AddSingleton<DevOpsCredentialManager>(provider =>
            {
                ILogger<DevOpsCredentialManager> logger = provider.GetService<ILogger<DevOpsCredentialManager>>() ??
                                                          loggerFactory.CreateLogger<DevOpsCredentialManager>();
                DevOpsEnvironmentInfo primaryEnv = devOpsEnvironments.First();
                return new DevOpsCredentialManager(primaryEnv, logger);
            });

            // Register multi-organization factory if multiple orgs found
            if (devOpsEnvironments.Count > 1)
            {
                services.AddSingleton<IMultiOrgDevOpsFactory>(provider =>
                {
                    ILogger<MultiOrgDevOpsFactory> logger = provider.GetService<ILogger<MultiOrgDevOpsFactory>>() ??
                                                            loggerFactory.CreateLogger<MultiOrgDevOpsFactory>();
                    return new MultiOrgDevOpsFactory(devOpsEnvironments, logger);
                });
            }

            // Register DevOps service using existing constructor pattern
            services.AddScoped<IDevOpsService>(provider =>
            {
                var manager = provider.GetRequiredService<DevOpsCredentialManager>();
                ILogger<DevOpsService> logger = provider.GetService<ILogger<DevOpsService>>() ??
                                                loggerFactory.CreateLogger<DevOpsService>();
                return new DevOpsService(manager, logger);
            });
        }
        else
        {
            // Register a service that provides helpful guidance
            services.AddSingleton<IDevOpsService>(provider =>
            {
                ILogger<DevOpsService> logger = provider.GetService<ILogger<DevOpsService>>() ??
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

public class MultiOrgDevOpsFactory(
    List<DevOpsEnvironmentInfo> environments,
    ILogger<MultiOrgDevOpsFactory> logger)
    : IMultiOrgDevOpsFactory
{
    private readonly ILogger<MultiOrgDevOpsFactory> _logger = logger;
    private readonly Dictionary<string, DevOpsCredentialManager> _managerCache = new();

    public List<string> GetDiscoveredOrganizations()
    {
        return environments.Select(e => e.OrganizationUrl).ToList();
    }

    public IDevOpsService CreateServiceForOrganization(string organizationUrl)
    {
        DevOpsCredentialManager manager = CreateManagerForOrganization(organizationUrl);
        ILogger<DevOpsService> logger = LoggerFactory.Create(b => b.AddDebug()).CreateLogger<DevOpsService>();
        return new DevOpsService(manager, logger);
    }

    public DevOpsCredentialManager CreateManagerForOrganization(string organizationUrl)
    {
        if (_managerCache.TryGetValue(organizationUrl, out DevOpsCredentialManager? cached))
            return cached;

        DevOpsEnvironmentInfo? environment = environments.FirstOrDefault(e => 
            e.OrganizationUrl.Equals(organizationUrl, StringComparison.OrdinalIgnoreCase));
        
        if (environment == null)
        {
            string available = string.Join(", ", environments.Select(e => e.OrganizationUrl));
            throw new InvalidOperationException(
                $"Organization '{organizationUrl}' not found in discovered environments. " +
                $"Available: {available}");
        }

        ILogger<DevOpsCredentialManager> logger = LoggerFactory.Create(b => b.AddDebug()).CreateLogger<DevOpsCredentialManager>();
        var manager = new DevOpsCredentialManager(environment, logger);

        _managerCache[organizationUrl] = manager;
        return manager;
    }
}

/// <summary>
/// Service that provides helpful guidance when no credentials are found
/// </summary>
public class NoCredentialsDevOpsService(ILogger<DevOpsService> logger) : IDevOpsService
{
    private readonly ILogger<DevOpsService> _logger = logger;

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
    
    public Task<IEnumerable<BuildDefinitionDto>> GetBuildDefinitionsAsync(string projectName)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<BuildDefinitionDto?> GetBuildDefinitionAsync(string projectName, int definitionId)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<IEnumerable<BuildDto>> GetBuildsAsync(string projectName, int? definitionId = null, int? top = null)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<BuildDto?> GetBuildAsync(string projectName, int buildId)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<BuildDto> QueueBuildAsync(string projectName, int definitionId, string? branch = null)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<IEnumerable<ReleaseDefinitionDto>> GetReleaseDefinitionsAsync(string projectName)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<ReleaseDefinitionDto?> GetReleaseDefinitionAsync(string projectName, int definitionId)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<IEnumerable<ReleaseDto>> GetReleasesAsync(string projectName, int? definitionId = null)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<string?> GetRepositoryFileContentAsync(string projectName, string repositoryName, string filePath, string? branch = null)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<bool> UpdateRepositoryFileAsync(string projectName, string repositoryName, string filePath, string content, string commitMessage, string? branch = null)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<IEnumerable<string>> FindYamlPipelineFilesAsync(string projectName, string repositoryName)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<string?> GetPipelineYamlAsync(string projectName, int definitionId)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }

    public Task<bool> UpdatePipelineYamlAsync(string projectName, int definitionId, string yamlContent, string commitMessage)
    {
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    }
    
    public Task<IEnumerable<BuildLogDto>> GetBuildLogsAsync(string projectName, int buildId) => 
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    
    public Task<BuildLogContentDto?> GetBuildLogContentAsync(string projectName, int buildId, int logId) => 
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    
    public Task<BuildTimelineDto?> GetBuildTimelineAsync(string projectName, int buildId) => 
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    
    public Task<IEnumerable<BuildStepLogDto>> GetBuildStepLogsAsync(string projectName, int buildId) => 
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    
    public Task<string> GetCompleteBuildLogAsync(string projectName, int buildId) => 
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    
    public Task<BuildLogContentDto?> GetBuildTaskLogAsync(string projectName, int buildId, string taskId) => 
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
    
    public Task<string> SearchBuildLogsWithRegexAsync(string projectName, int buildId, string regexPattern, int contextLines = 3, bool caseSensitive = false, int maxMatches = 50) => 
        throw new InvalidOperationException("No Azure DevOps credentials discovered. Please set up credentials first.");
}