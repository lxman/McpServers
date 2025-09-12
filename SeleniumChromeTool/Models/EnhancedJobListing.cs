using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SeleniumChromeTool.Services.Enhanced;

namespace SeleniumChromeTool.Models;

public class EnhancedJobListing
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    public string JobId { get; set; } = string.Empty; // External job ID for deduplication
    public string JobUrl { get; set; } = string.Empty; // For deduplication
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Salary { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime DatePosted { get; set; }
    public DateTime? PostedDate { get; set; } // Nullable version for better handling
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
    public bool IsRemote { get; set; }
    public string JobType { get; set; } = string.Empty; // Full-time, Contract, etc.
    public string ExperienceLevel { get; set; } = string.Empty;
    public List<string> RequiredSkills { get; set; } = [];
    public List<string> PreferredSkills { get; set; } = [];
    public List<string> Technologies { get; set; } = [];
    public List<string> Requirements { get; set; } = []; // For market intelligence analysis
    public string Benefits { get; set; } = string.Empty;
    public JobSite SourceSite { get; set; }
    public double MatchScore { get; set; } // Against user profile
    public bool IsApplied { get; set; }
    public string Notes { get; set; } = string.Empty;
    
    // Phase 2 Enhancement Fields
    public DuplicateJobInfo? DuplicateInfo { get; set; } // Smart deduplication data
    public ApplicationCategoryInfo? ApplicationCategory { get; set; } // Application management data
    public MarketIntelligenceData? MarketData { get; set; } // Market intelligence data
}

/// <summary>
/// Application categorization information attached to jobs
/// </summary>
public class ApplicationCategoryInfo
{
    public ApplicationPriority Priority { get; set; }
    public int ApplicationReadinessScore { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
    public List<string> ActionItems { get; set; } = [];
    public TimeSpan EstimatedApplicationTime { get; set; }
    public UrgencyLevel DeadlineUrgency { get; set; }
    public CompetitivenessLevel CompetitivenessRating { get; set; }
    public DateTime CategorizedAt { get; set; }
}

/// <summary>
/// Market intelligence data attached to jobs
/// </summary>
public class MarketIntelligenceData
{
    public decimal SalaryPercentile { get; set; } // Where this salary ranks in market
    public List<string> TrendingTechnologies { get; set; } = []; // Technologies in this job that are trending
    public CompanyMarketInfo? CompanyInfo { get; set; }
    public LocationMarketInfo? LocationInfo { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

/// <summary>
/// Company market information
/// </summary>
public class CompanyMarketInfo
{
    public HiringVelocity HiringVelocity { get; set; }
    public int TotalJobPostings { get; set; }
    public double RemoteJobPercentage { get; set; }
    public decimal AverageCompanySalary { get; set; }
    public string CompanySize { get; set; } = string.Empty; // Startup, Mid-Size, Enterprise
}

/// <summary>
/// Location market information
/// </summary>
public class LocationMarketInfo
{
    public int JobCountInLocation { get; set; }
    public decimal AverageLocationSalary { get; set; }
    public double RemotePercentage { get; set; }
    public List<string> TopCompaniesInLocation { get; set; } = [];
}