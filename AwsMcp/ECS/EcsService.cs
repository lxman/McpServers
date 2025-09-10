using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Runtime;
using AwsMcp.Configuration;
using Microsoft.Extensions.Logging;

namespace AwsMcp.ECS;

/// <summary>
/// Service for ECS operations
/// </summary>
public class EcsService
{
    private readonly ILogger<EcsService> _logger;
    private AmazonECSClient? _ecsClient;
    private AwsConfiguration? _config;
    
    public EcsService(ILogger<EcsService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Initialize ECS client with configuration
    /// </summary>
    public async Task<bool> InitializeAsync(AwsConfiguration config)
    {
        try
        {
            _config = config;
            
            var clientConfig = new AmazonECSConfig
            {
                RegionEndpoint = config.GetRegionEndpoint(),
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
                MaxErrorRetry = config.MaxRetryAttempts,
                UseHttp = !config.UseHttps
            };
            
            // Set custom endpoint if provided (for LocalStack, etc.)
            if (!string.IsNullOrEmpty(config.ServiceUrl))
            {
                clientConfig.ServiceURL = config.ServiceUrl;
            }
            
            var credentialsProvider = new AwsCredentialsProvider(config);
            AWSCredentials? credentials = credentialsProvider.GetCredentials();
            
            if (credentials != null)
            {
                _ecsClient = new AmazonECSClient(credentials, clientConfig);
            }
            else
            {
                _ecsClient = new AmazonECSClient(clientConfig);
            }
            
            // Test connection by listing clusters
            await _ecsClient.ListClustersAsync(new ListClustersRequest());
            
            _logger.LogInformation("ECS client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ECS client");
            return false;
        }
    }
    
    /// <summary>
    /// List all clusters
    /// </summary>
    public async Task<ListClustersResponse> ListClustersAsync()
    {
        EnsureInitialized();
        return await _ecsClient!.ListClustersAsync(new ListClustersRequest());
    }
    
    /// <summary>
    /// Describe clusters
    /// </summary>
    public async Task<DescribeClustersResponse> DescribeClustersAsync(List<string>? clusterArns = null)
    {
        EnsureInitialized();
        
        var request = new DescribeClustersRequest();
        if (clusterArns != null && clusterArns.Any())
        {
            request.Clusters = clusterArns;
        }
        
        return await _ecsClient!.DescribeClustersAsync(request);
    }
    
    /// <summary>
    /// Create a new cluster
    /// </summary>
    public async Task<CreateClusterResponse> CreateClusterAsync(string clusterName, List<Tag>? tags = null)
    {
        EnsureInitialized();
        
        var request = new CreateClusterRequest
        {
            ClusterName = clusterName
        };
        
        if (tags != null && tags.Any())
        {
            request.Tags = tags;
        }
        
        return await _ecsClient!.CreateClusterAsync(request);
    }
    
    /// <summary>
    /// Delete a cluster
    /// </summary>
    public async Task<DeleteClusterResponse> DeleteClusterAsync(string cluster)
    {
        EnsureInitialized();
        return await _ecsClient!.DeleteClusterAsync(new DeleteClusterRequest { Cluster = cluster });
    }
    
    /// <summary>
    /// List services in a cluster
    /// </summary>
    public async Task<ListServicesResponse> ListServicesAsync(string? cluster = null)
    {
        EnsureInitialized();
        
        var request = new ListServicesRequest();
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        return await _ecsClient!.ListServicesAsync(request);
    }
    
    /// <summary>
    /// Describe services
    /// </summary>
    public async Task<DescribeServicesResponse> DescribeServicesAsync(List<string> services, string? cluster = null)
    {
        EnsureInitialized();
        
        var request = new DescribeServicesRequest
        {
            Services = services
        };
        
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        return await _ecsClient!.DescribeServicesAsync(request);
    }
    
    /// <summary>
    /// List tasks in a cluster
    /// </summary>
    public async Task<ListTasksResponse> ListTasksAsync(string? cluster = null, string? serviceName = null)
    {
        EnsureInitialized();
        
        var request = new ListTasksRequest();
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        if (!string.IsNullOrEmpty(serviceName))
        {
            request.ServiceName = serviceName;
        }
        
        return await _ecsClient!.ListTasksAsync(request);
    }
    
    /// <summary>
    /// Describe tasks
    /// </summary>
    public async Task<DescribeTasksResponse> DescribeTasksAsync(List<string> tasks, string? cluster = null)
    {
        EnsureInitialized();
        
        var request = new DescribeTasksRequest
        {
            Tasks = tasks
        };
        
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        return await _ecsClient!.DescribeTasksAsync(request);
    }
    
    /// <summary>
    /// List task definitions
    /// </summary>
    public async Task<ListTaskDefinitionsResponse> ListTaskDefinitionsAsync(string? familyPrefix = null)
    {
        EnsureInitialized();
        
        var request = new ListTaskDefinitionsRequest();
        if (!string.IsNullOrEmpty(familyPrefix))
        {
            request.FamilyPrefix = familyPrefix;
        }
        
        return await _ecsClient!.ListTaskDefinitionsAsync(request);
    }
    
    /// <summary>
    /// Describe task definition
    /// </summary>
    public async Task<DescribeTaskDefinitionResponse> DescribeTaskDefinitionAsync(string taskDefinition)
    {
        EnsureInitialized();
        return await _ecsClient!.DescribeTaskDefinitionAsync(new DescribeTaskDefinitionRequest 
        { 
            TaskDefinition = taskDefinition 
        });
    }
    
    /// <summary>
    /// Run a task
    /// </summary>
    public async Task<RunTaskResponse> RunTaskAsync(string taskDefinition, string? cluster = null, 
        int count = 1, string launchType = "EC2")
    {
        EnsureInitialized();
        
        var request = new RunTaskRequest
        {
            TaskDefinition = taskDefinition,
            Count = count,
            LaunchType = launchType
        };
        
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        return await _ecsClient!.RunTaskAsync(request);
    }
    
    /// <summary>
    /// Stop a task
    /// </summary>
    public async Task<StopTaskResponse> StopTaskAsync(string task, string? cluster = null, string? reason = null)
    {
        EnsureInitialized();
        
        var request = new StopTaskRequest
        {
            Task = task
        };
        
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        if (!string.IsNullOrEmpty(reason))
        {
            request.Reason = reason;
        }
        
        return await _ecsClient!.StopTaskAsync(request);
    }
    
    /// <summary>
    /// Update service
    /// </summary>
    public async Task<UpdateServiceResponse> UpdateServiceAsync(string service, string? cluster = null, 
        int? desiredCount = null, string? taskDefinition = null)
    {
        EnsureInitialized();
        
        var request = new UpdateServiceRequest
        {
            Service = service
        };
        
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        if (desiredCount.HasValue)
        {
            request.DesiredCount = desiredCount.Value;
        }
        
        if (!string.IsNullOrEmpty(taskDefinition))
        {
            request.TaskDefinition = taskDefinition;
        }
        
        return await _ecsClient!.UpdateServiceAsync(request);
    }
    
    /// <summary>
    /// Get container instances
    /// </summary>
    public async Task<List<ContainerInstance>> ListContainerInstancesAsync(string? cluster = null)
    {
        EnsureInitialized();
        
        var request = new ListContainerInstancesRequest();
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        ListContainerInstancesResponse? response = await _ecsClient!.ListContainerInstancesAsync(request);
        
        if (response.ContainerInstanceArns.Any())
        {
            var describeRequest = new DescribeContainerInstancesRequest
            {
                ContainerInstances = response.ContainerInstanceArns
            };
            
            if (!string.IsNullOrEmpty(cluster))
            {
                describeRequest.Cluster = cluster;
            }
            
            DescribeContainerInstancesResponse? describeResponse = await _ecsClient!.DescribeContainerInstancesAsync(describeRequest);
            return describeResponse.ContainerInstances;
        }
        
        return new List<ContainerInstance>();
    }
    
    private void EnsureInitialized()
    {
        if (_ecsClient == null)
        {
            throw new InvalidOperationException("ECS client is not initialized. Call InitializeAsync first.");
        }
    }
    
    public void Dispose()
    {
        _ecsClient?.Dispose();
    }
}
