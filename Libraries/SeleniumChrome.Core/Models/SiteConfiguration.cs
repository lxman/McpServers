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
}