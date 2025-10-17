using Amazon.ECS;
using Amazon.ECS.Model;
using AwsServer.Configuration;
using Task = System.Threading.Tasks.Task;

namespace AwsServer.ECS;

/// <summary>
/// Service for ECS operations
/// </summary>
public class EcsService
{
    private readonly ILogger<EcsService> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonECSClient? _ecsClient;
    private AwsConfiguration? _config;
    private bool _isInitialized;
    
    public EcsService(
        ILogger<EcsService> logger,
        AwsDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        
        _ = Task.Run(AutoInitializeAsync);
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
            var credentials = credentialsProvider.GetCredentials();
            
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
        await EnsureInitializedAsync();
        return await _ecsClient!.ListClustersAsync(new ListClustersRequest());
    }
    
    /// <summary>
    /// Describe clusters
    /// </summary>
    public async Task<DescribeClustersResponse> DescribeClustersAsync(List<string>? clusterArns = null)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeClustersRequest();
        if (clusterArns != null && clusterArns.Count != 0)
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
        await EnsureInitializedAsync();
        
        var request = new CreateClusterRequest
        {
            ClusterName = clusterName
        };
        
        if (tags != null && tags.Count != 0)
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
        await EnsureInitializedAsync();
        return await _ecsClient!.DeleteClusterAsync(new DeleteClusterRequest { Cluster = cluster });
    }
    
    /// <summary>
    /// List services in a cluster
    /// </summary>
    public async Task<ListServicesResponse> ListServicesAsync(string? cluster = null)
    {
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
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
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
        
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
        await EnsureInitializedAsync();
        
        var request = new ListContainerInstancesRequest();
        if (!string.IsNullOrEmpty(cluster))
        {
            request.Cluster = cluster;
        }
        
        var response = await _ecsClient!.ListContainerInstancesAsync(request);
        
        if (response.ContainerInstanceArns.Count != 0)
        {
            var describeRequest = new DescribeContainerInstancesRequest
            {
                ContainerInstances = response.ContainerInstanceArns
            };
            
            if (!string.IsNullOrEmpty(cluster))
            {
                describeRequest.Cluster = cluster;
            }
            
            var describeResponse = await _ecsClient!.DescribeContainerInstancesAsync(describeRequest);
            return describeResponse.ContainerInstances;
        }
        
        return [];
    }
    
    /// <summary>
    /// Auto-initialize with discovered credentials
    /// </summary>
    private async Task AutoInitializeAsync()
    {
        try
        {
            if (_discoveryService.AutoInitialize())
            {
                var accountInfo = await _discoveryService.GetAccountInfoAsync();
                
                var config = new AwsConfiguration
                {
                    Region = accountInfo.InferredRegion,
                    ProfileName = Environment.GetEnvironmentVariable("AWS_PROFILE") ?? "default"
                };
                
                await InitializeAsync(config);
                _isInitialized = true;
                _logger.LogInformation("S3 service auto-initialized successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-initialize S3 service. Explicit initialization may be required.");
        }
    }
    
    /// <summary>
    /// Ensure the service is initialized (wait if auto-initialization is still running)
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        // Wait for auto-initialization to complete
        if (!_isInitialized && _ecsClient == null)
        {
            // Wait up to 5 seconds for auto-initialization
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!_isInitialized && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }
            
            if (_ecsClient == null)
            {
                throw new InvalidOperationException(
                    "S3 client could not be auto-initialized. Please ensure AWS credentials are configured properly or call Initialize explicitly.");
            }
        }
    }
    
    public void Dispose()
    {
        _ecsClient?.Dispose();
    }
}
