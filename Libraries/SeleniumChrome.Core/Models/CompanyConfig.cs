using System.ComponentModel.DataAnnotations;

namespace SeleniumChrome.Core.Models;

public class CompanyConfig
{
    [Required]
    public string CompanyName { get; set; } = string.Empty;
    
    [Required]
    public string CareersUrl { get; set; } = string.Empty;
    
    public string? SearchUrl { get; set; }
    
    [Required]
    public CompanySelectors Selectors { get; set; } = new();
    
    public CompanyMetadata Metadata { get; set; } = new();
    
    public ScrapingConfig ScrapingConfig { get; set; } = new();
}

public class CompanySelectors
{
    public string JobContainer { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string JobLocation { get; set; } = string.Empty;
    public string JobDepartment { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public string ApplyLink { get; set; } = string.Empty;
    public string PostedDate { get; set; } = string.Empty;
    public string NextPageButton { get; set; } = string.Empty;
    public string LoadMoreButton { get; set; } = string.Empty;
}

public class CompanyMetadata
{
    public int EmployeeCount { get; set; }
    public string[] TechStack { get; set; } = [];
    public string RemotePolicy { get; set; } = string.Empty;
    public string CompanySize { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public bool IsRemoteFriendly { get; set; }
    public string[] TechKeywords { get; set; } = [];
}

public class ScrapingConfig
{
    public int DelayBetweenRequests { get; set; } = 3000; // 3 seconds
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public bool RequiresJavaScript { get; set; } = true;
    public bool RequiresScroll { get; set; } = false;
    public string? SearchFilters { get; set; }
    public string[] RequiredTechKeywords { get; set; } = [];
}
