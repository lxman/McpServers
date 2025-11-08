using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SeleniumChrome.Core.Models;

public class SiteConfiguration
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string SiteName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string JobsUrl { get; set; } = string.Empty;
    public string SearchEndpoint { get; set; } = string.Empty;
    public string[] SupportedSearchTerms { get; set; } = [];
    public Dictionary<string, string> CssSelectors { get; set; } = new();
    public Dictionary<string, string> Selectors { get; set; } = new();
    public Dictionary<string, string> UrlParameters { get; set; } = new();
    public RateLimitConfig RateLimitConfig { get; set; } = new();
    public RateLimitConfig RateLimit { get; set; } = new();
    public AntiDetectionConfig AntiDetection { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool RequiresJavaScript { get; set; } = false;
    public DateTime LastUpdated { get; set; }
    public string Notes { get; set; } = string.Empty;

    // Extended fields from MongoDB schema
    [BsonIgnoreIfDefault]
    public string ScrapingMethod { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public Dictionary<string, string> ApiEndpoints { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, string> WebToolSelectors { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, string> Workflow { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> DataStructure { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> OutputFormat { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> TestResults { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> ApiValidation { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> CurrentWorkflowValidated { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> SearchValidation { get; set; } = new();

    [BsonIgnoreIfDefault]
    public string CurrentMethod { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public string PreferredMethod { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public string SearchPattern { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public DateTime LastTested { get; set; }

    // Additional complex fields found in various site configurations
    [BsonIgnoreIfDefault]
    public Dictionary<string, object> ApiResponseStructure { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> FilteringOptions { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> ApiTerms { get; set; } = new();

    [BsonIgnoreIfDefault]
    public List<string> AdvantagesOfDirectAPI { get; set; } = [];

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> SearchFormStructure { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> CloudflareProtection { get; set; } = new();

    [BsonIgnoreIfDefault]
    public string RecommendedApproach { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> AlternativeAccess { get; set; } = new();

    [BsonIgnoreIfDefault]
    public string WebToolCompatibility { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> JobListingStructure { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> JobCardSelectors { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> JobDetailPageStructure { get; set; } = new();

    [BsonIgnoreIfDefault]
    public List<object> SampleJobLinks { get; set; } = [];

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> TechnicalFramework { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> AccessibilityStatus { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> DataExtractionCapability { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> WorkflowForWebTool { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> ComparisonWithOtherTools { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> DataQuality { get; set; } = new();

    [BsonIgnoreIfDefault]
    public string JobFocus { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public List<string> AdvantagesOverCompetitors { get; set; } = [];

    [BsonIgnoreIfDefault]
    public string AuthenticationLevel { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public string JavaScriptDependency { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> HomepageAnalysis { get; set; } = new();

    [BsonIgnoreIfDefault]
    public string PotentialValue { get; set; } = string.Empty;

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> RetryStrategy { get; set; } = new();

    [BsonIgnoreIfDefault]
    public Dictionary<string, object> TechnicalIssuesDetected { get; set; } = new();

    [BsonIgnoreIfDefault]
    public string SearchUrlPattern { get; set; } = string.Empty;
}