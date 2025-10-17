using AzureServer.Authentication;
using AzureServer.Authentication.models;
using AzureServer.Services.AppService;
using AzureServer.Services.Core;
using AzureServer.Services.CostManagement;
using AzureServer.Services.DevOps;
using AzureServer.Services.DevOps.Models;
using AzureServer.Services.EventHubs;
using AzureServer.Services.KeyVault;
using AzureServer.Services.Monitor;
using AzureServer.Services.ResourceManagement;
using AzureServer.Services.ServiceBus;
using AzureServer.Services.Sql.DbManagement;
using AzureServer.Services.Sql.QueryExecution;
using AzureServer.Services.Storage;

namespace AzureServer.Configuration;

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

        // Register credential discovery and selection services
        services.AddSingleton<CredentialDiscoveryService>(provider =>
        {
            var logger = provider.GetService<ILogger<CredentialDiscoveryService>>() ??
                         loggerFactory.CreateLogger<CredentialDiscoveryService>();
            return new CredentialDiscoveryService(logger);
        });

        services.AddSingleton<CredentialSelectionService>(provider =>
        {
            var logger = provider.GetService<ILogger<CredentialSelectionService>>() ??
                         loggerFactory.CreateLogger<CredentialSelectionService>();
            var discoveryService = provider.GetRequiredService<CredentialDiscoveryService>();
            return new CredentialSelectionService(logger, discoveryService);
        });

        // Register ArmClientFactory as a singleton for managing ArmClient instances
        services.AddSingleton<ArmClientFactory>(provider =>
        {
            var logger = provider.GetService<ILogger<ArmClientFactory>>() ??
                         loggerFactory.CreateLogger<ArmClientFactory>();
            var credentialService = provider.GetRequiredService<CredentialSelectionService>();
            return new ArmClientFactory(credentialService, logger);
        });


        // Register Entra authentication services
        services.AddSingleton<EntraAuthConfigLoader>(provider =>
        {
            var logger = provider.GetService<ILogger<EntraAuthConfigLoader>>() ??
                         loggerFactory.CreateLogger<EntraAuthConfigLoader>();
            return new EntraAuthConfigLoader(logger);
        });

        services.AddSingleton<EntraAuthConfig>(provider =>
        {
            var configLoader = provider.GetRequiredService<EntraAuthConfigLoader>();
            return configLoader.LoadConfiguration();
        });

        services.AddSingleton<EntraCredentialService>(provider =>
        {
            var logger = provider.GetService<ILogger<EntraCredentialService>>() ??
                         loggerFactory.CreateLogger<EntraCredentialService>();
            var config = provider.GetRequiredService<EntraAuthConfig>();
            return new EntraCredentialService(logger, config);
        });

        // Discover Azure DevOps environments only (ARM credentials now handled by CredentialSelectionService)
        var discoveryLogger = loggerFactory.CreateLogger<AzureEnvironmentDiscovery>();
        var discovery = new AzureEnvironmentDiscovery(discoveryLogger);
        
        // Safe discovery with graceful error handling
        List<DevOpsEnvironmentInfo> devOpsEnvironments;
        try
        {
            devOpsEnvironments = await discovery.DiscoverDevOpsEnvironmentsAsync();
            if (devOpsEnvironments.Count > 0)
            {
                discoveryLogger.LogInformation("Successfully discovered {Count} Azure DevOps environment(s)", devOpsEnvironments.Count);
            }
            else
            {
                discoveryLogger.LogInformation("No Azure DevOps environments discovered. DevOps services will return helpful guidance.");
            }
        }
        catch (Exception ex)
        {
            discoveryLogger.LogWarning(ex, "Failed to discover Azure DevOps environments. DevOps services will be unavailable but app will continue running.");
            devOpsEnvironments = new List<DevOpsEnvironmentInfo>();
        }


        // Configure Azure Resource Management service using ArmClientFactory
        services.AddScoped<IResourceManagementService>(provider =>
        {
            var logger = provider.GetService<ILogger<ResourceManagementService>>() ??
                         loggerFactory.CreateLogger<ResourceManagementService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ResourceManagementService(armClientFactory, logger);
        });

        // Configure Azure Cost Management service using CredentialSelectionService
        services.AddScoped<ICostManagementService>(provider =>
        {
            var logger = provider.GetService<ILogger<CostManagementService>>() ??
                         loggerFactory.CreateLogger<CostManagementService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new CostManagementService(armClientFactory, logger);
        });

        // Configure Azure Storage service using CredentialSelectionService
        services.AddScoped<IStorageService>(provider =>
        {
            var logger = provider.GetService<ILogger<StorageService>>() ??
                         loggerFactory.CreateLogger<StorageService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new StorageService(armClientFactory, logger);
        });

        // Configure Azure File Storage service using CredentialSelectionService (doesn't use ArmClient)
        services.AddScoped<IFileStorageService>(provider =>
        {
            var logger = provider.GetService<ILogger<FileStorageService>>() ??
                         loggerFactory.CreateLogger<FileStorageService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new FileStorageService(armClientFactory, logger);
        });

        // Configure Azure Key Vault service using CredentialSelectionService (doesn't use ArmClient)
        services.AddScoped<IKeyVaultService>(provider =>
        {
            var logger = provider.GetService<ILogger<KeyVaultService>>() ??
                         loggerFactory.CreateLogger<KeyVaultService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new KeyVaultService(armClientFactory, logger);
        });

        // Configure Azure SQL Database Management service using CredentialSelectionService
        services.AddScoped<ISqlDatabaseService>(provider =>
        {
            var logger = provider.GetService<ILogger<SqlDatabaseService>>() ??
                         loggerFactory.CreateLogger<SqlDatabaseService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SqlDatabaseService(armClientFactory, logger);
        });

        // Configure SQL Query Execution service using CredentialSelectionService (doesn't use ArmClient)
        services.AddScoped<ISqlQueryService>(provider =>
        {
            var logger = provider.GetService<ILogger<SqlQueryService>>() ??
                         loggerFactory.CreateLogger<SqlQueryService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new SqlQueryService(armClientFactory, logger);
        });
        
        // Configure Azure App Service using ArmClientFactory
        services.AddScoped<IAppServiceService>(provider =>
        {
            var logger = provider.GetService<ILogger<AppServiceService>>() ??
                         loggerFactory.CreateLogger<AppServiceService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new AppServiceService(armClientFactory, logger);
        });

        // Configure Azure Monitor service using ArmClientFactory
        services.AddScoped<IMonitorService>(provider =>
        {
            var logger = provider.GetService<ILogger<MonitorService>>() ??
                         loggerFactory.CreateLogger<MonitorService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new MonitorService(armClientFactory, logger);
        });

        // Configure Azure Service Bus service using ArmClientFactory
        services.AddScoped<IServiceBusService>(provider =>
        {
            var logger = provider.GetService<ILogger<ServiceBusService>>() ??
                         loggerFactory.CreateLogger<ServiceBusService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new ServiceBusService(armClientFactory, logger);
        });

        // Configure Azure Event Hubs service using ArmClientFactory
        services.AddScoped<IEventHubsService>(provider =>
        {
            var logger = provider.GetService<ILogger<EventHubsService>>() ??
                         loggerFactory.CreateLogger<EventHubsService>();
            var armClientFactory = provider.GetRequiredService<ArmClientFactory>();
            return new EventHubsService(armClientFactory, logger);
        });


        services.AddNetworkingServices(loggerFactory);

        // Configure Azure DevOps services
        if (devOpsEnvironments.Count > 0)
        {
            // Register DevOps credential manager for primary organization
            services.AddSingleton<DevOpsCredentialManager>(provider =>
            {
                var logger = provider.GetService<ILogger<DevOpsCredentialManager>>() ??
                             loggerFactory.CreateLogger<DevOpsCredentialManager>();
                var primaryEnv = devOpsEnvironments.First();
                return new DevOpsCredentialManager(primaryEnv, logger);
            });

            // Register multi-organization factory if multiple orgs found
            if (devOpsEnvironments.Count > 1)
            {
                services.AddSingleton<IMultiOrgDevOpsFactory>(provider =>
                {
                    var logger = provider.GetService<ILogger<MultiOrgDevOpsFactory>>() ??
                                 loggerFactory.CreateLogger<MultiOrgDevOpsFactory>();
                    return new MultiOrgDevOpsFactory(devOpsEnvironments, logger);
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
        var manager = CreateManagerForOrganization(organizationUrl);
        var logger = LoggerFactory.Create(b => b.AddDebug()).CreateLogger<DevOpsService>();
        return new DevOpsService(manager, logger);
    }

    public DevOpsCredentialManager CreateManagerForOrganization(string organizationUrl)
    {
        if (_managerCache.TryGetValue(organizationUrl, out var cached))
            return cached;

        var environment = environments.FirstOrDefault(e => 
            e.OrganizationUrl.Equals(organizationUrl, StringComparison.OrdinalIgnoreCase));
        
        if (environment is null)
        {
            var available = string.Join(", ", environments.Select(e => e.OrganizationUrl));
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