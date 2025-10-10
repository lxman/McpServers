using Amazon.QuickSight;
using Amazon.QuickSight.Model;
using Amazon.Runtime;
using AwsMcp.Configuration;
using Microsoft.Extensions.Logging;
using AccountInfo = AwsMcp.Configuration.Models.AccountInfo;

namespace AwsMcp.QuickSight;

/// <summary>
/// Service for QuickSight operations
/// </summary>
public class QuickSightService
{
    private readonly ILogger<QuickSightService> _logger;
    private readonly AwsDiscoveryService _discoveryService;
    private AmazonQuickSightClient? _quickSightClient;
    private AwsConfiguration? _config;
    private bool _isInitialized;
    
    public QuickSightService(
        ILogger<QuickSightService> logger,
        AwsDiscoveryService discoveryService)
    {
        _logger = logger;
        _discoveryService = discoveryService;
        
        _ = Task.Run(AutoInitializeAsync);
    }
    
    /// <summary>
    /// Initialize QuickSight client with configuration
    /// </summary>
    public async Task<bool> InitializeAsync(AwsConfiguration config)
    {
        try
        {
            _config = config;
            
            var clientConfig = new AmazonQuickSightConfig
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
                _quickSightClient = new AmazonQuickSightClient(credentials, clientConfig);
            }
            else
            {
                _quickSightClient = new AmazonQuickSightClient(clientConfig);
            }
            
            _isInitialized = true;
            _logger.LogInformation("QuickSight client initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize QuickSight client");
            _isInitialized = false;
            return false;
        }
    }
    
    /// <summary>
    /// Auto-initialize using discovery service
    /// </summary>
    private async Task AutoInitializeAsync()
    {
        try
        {
            if (_discoveryService.AutoInitialize())
            {
                AccountInfo accountInfo = await _discoveryService.GetAccountInfoAsync();
                
                var config = new AwsConfiguration
                {
                    Region = accountInfo.InferredRegion,
                    AccessKeyId = null,
                    SecretAccessKey = null
                };
                
                await InitializeAsync(config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Auto-initialization of QuickSight client failed");
        }
    }
    
    /// <summary>
    /// Ensure the client is initialized
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized || _quickSightClient == null)
        {
            await AutoInitializeAsync();
            
            if (!_isInitialized || _quickSightClient == null)
            {
                throw new InvalidOperationException(
                    "QuickSight client is not initialized. Call InitializeQuickSight first.");
            }
        }
    }
    
    /// <summary>
    /// List dashboards in an AWS account
    /// </summary>
    public async Task<ListDashboardsResponse> ListDashboardsAsync(string awsAccountId, int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new ListDashboardsRequest
        {
            AwsAccountId = awsAccountId,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.ListDashboardsAsync(request);
    }
    
    /// <summary>
    /// Describe a specific dashboard
    /// </summary>
    public async Task<DescribeDashboardResponse> DescribeDashboardAsync(string awsAccountId, string dashboardId)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeDashboardRequest
        {
            AwsAccountId = awsAccountId,
            DashboardId = dashboardId
        };
        
        return await _quickSightClient!.DescribeDashboardAsync(request);
    }
    
    /// <summary>
    /// List data sets
    /// </summary>
    public async Task<ListDataSetsResponse> ListDataSetsAsync(string awsAccountId, int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new ListDataSetsRequest
        {
            AwsAccountId = awsAccountId,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.ListDataSetsAsync(request);
    }
    
    /// <summary>
    /// Describe a specific data set
    /// </summary>
    public async Task<DescribeDataSetResponse> DescribeDataSetAsync(string awsAccountId, string dataSetId)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeDataSetRequest
        {
            AwsAccountId = awsAccountId,
            DataSetId = dataSetId
        };
        
        return await _quickSightClient!.DescribeDataSetAsync(request);
    }
    
    /// <summary>
    /// List data sources
    /// </summary>
    public async Task<ListDataSourcesResponse> ListDataSourcesAsync(string awsAccountId, int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new ListDataSourcesRequest
        {
            AwsAccountId = awsAccountId,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.ListDataSourcesAsync(request);
    }
    
    /// <summary>
    /// Describe a specific data source
    /// </summary>
    public async Task<DescribeDataSourceResponse> DescribeDataSourceAsync(string awsAccountId, string dataSourceId)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeDataSourceRequest
        {
            AwsAccountId = awsAccountId,
            DataSourceId = dataSourceId
        };
        
        return await _quickSightClient!.DescribeDataSourceAsync(request);
    }
    
    /// <summary>
    /// List analyses
    /// </summary>
    public async Task<ListAnalysesResponse> ListAnalysesAsync(string awsAccountId, int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new ListAnalysesRequest
        {
            AwsAccountId = awsAccountId,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.ListAnalysesAsync(request);
    }
    
    /// <summary>
    /// Describe a specific analysis
    /// </summary>
    public async Task<DescribeAnalysisResponse> DescribeAnalysisAsync(string awsAccountId, string analysisId)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeAnalysisRequest
        {
            AwsAccountId = awsAccountId,
            AnalysisId = analysisId
        };
        
        return await _quickSightClient!.DescribeAnalysisAsync(request);
    }
    
    /// <summary>
    /// List users in QuickSight
    /// </summary>
    public async Task<ListUsersResponse> ListUsersAsync(string awsAccountId, string awsNamespace = "default", int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new ListUsersRequest
        {
            AwsAccountId = awsAccountId,
            Namespace = awsNamespace,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.ListUsersAsync(request);
    }
    
    /// <summary>
    /// Describe a specific user
    /// </summary>
    public async Task<DescribeUserResponse> DescribeUserAsync(string awsAccountId, string userName, string awsNamespace = "default")
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeUserRequest
        {
            AwsAccountId = awsAccountId,
            UserName = userName,
            Namespace = awsNamespace
        };
        
        return await _quickSightClient!.DescribeUserAsync(request);
    }
    
    /// <summary>
    /// Generate an embed URL for a dashboard
    /// </summary>
    public async Task<GenerateEmbedUrlForAnonymousUserResponse> GenerateEmbedUrlForAnonymousUserAsync(
        string awsAccountId, 
        string awsNamespace,
        List<string> authorizedResourceArns,
        List<SessionTag>? sessionTags = null,
        long sessionLifetimeInMinutes = 600)
    {
        await EnsureInitializedAsync();
        
        var request = new GenerateEmbedUrlForAnonymousUserRequest
        {
            AwsAccountId = awsAccountId,
            Namespace = awsNamespace,
            AuthorizedResourceArns = authorizedResourceArns,
            SessionLifetimeInMinutes = sessionLifetimeInMinutes
        };
        
        if (sessionTags != null && sessionTags.Count > 0)
        {
            request.SessionTags = sessionTags;
        }
        
        // Set experience configuration for anonymous embedding
        request.ExperienceConfiguration = new AnonymousUserEmbeddingExperienceConfiguration
        {
            Dashboard = new AnonymousUserDashboardEmbeddingConfiguration
            {
                InitialDashboardId = authorizedResourceArns.FirstOrDefault()?.Split('/').LastOrDefault() ?? ""
            }
        };
        
        return await _quickSightClient!.GenerateEmbedUrlForAnonymousUserAsync(request);
    }
    
    /// <summary>
    /// List themes
    /// </summary>
    public async Task<ListThemesResponse> ListThemesAsync(string awsAccountId, int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new ListThemesRequest
        {
            AwsAccountId = awsAccountId,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.ListThemesAsync(request);
    }
    
    /// <summary>
    /// Describe account settings
    /// </summary>
    public async Task<DescribeAccountSettingsResponse> DescribeAccountSettingsAsync(string awsAccountId)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeAccountSettingsRequest
        {
            AwsAccountId = awsAccountId
        };
        
        return await _quickSightClient!.DescribeAccountSettingsAsync(request);
    }
    
    /// <summary>
    /// List folders
    /// </summary>
    public async Task<ListFoldersResponse> ListFoldersAsync(string awsAccountId, int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new ListFoldersRequest
        {
            AwsAccountId = awsAccountId,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.ListFoldersAsync(request);
    }
    
    /// <summary>
    /// Describe a specific folder
    /// </summary>
    public async Task<DescribeFolderResponse> DescribeFolderAsync(string awsAccountId, string folderId)
    {
        await EnsureInitializedAsync();
        
        var request = new DescribeFolderRequest
        {
            AwsAccountId = awsAccountId,
            FolderId = folderId
        };
        
        return await _quickSightClient!.DescribeFolderAsync(request);
    }
    
    /// <summary>
    /// Search dashboards
    /// </summary>
    public async Task<SearchDashboardsResponse> SearchDashboardsAsync(
        string awsAccountId, 
        List<DashboardSearchFilter> filters, 
        int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new SearchDashboardsRequest
        {
            AwsAccountId = awsAccountId,
            Filters = filters,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.SearchDashboardsAsync(request);
    }
    
    /// <summary>
    /// Search analyses
    /// </summary>
    public async Task<SearchAnalysesResponse> SearchAnalysesAsync(
        string awsAccountId, 
        List<AnalysisSearchFilter> filters, 
        int maxResults = 100)
    {
        await EnsureInitializedAsync();
        
        var request = new SearchAnalysesRequest
        {
            AwsAccountId = awsAccountId,
            Filters = filters,
            MaxResults = maxResults
        };
        
        return await _quickSightClient!.SearchAnalysesAsync(request);
    }
}