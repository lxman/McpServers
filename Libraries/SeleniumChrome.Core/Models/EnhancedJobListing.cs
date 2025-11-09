using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SeleniumChrome.Core.Services.Enhanced;

namespace SeleniumChrome.Core.Models;

public class EnhancedJobListing
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("userId")]
    [BsonIgnoreIfDefault]
    public string UserId { get; set; } = string.Empty; // User who saved/owns this job listing

    [BsonElement("jobId")]
    public string JobId { get; set; } = string.Empty; // External job ID for deduplication

    [BsonElement("jobUrl")]
    public string JobUrl { get; set; } = string.Empty; // For deduplication

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("company")]
    public string Company { get; set; } = string.Empty;

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty;

    [BsonElement("salary")]
    [BsonIgnoreIfDefault]
    public string Salary { get; set; } = string.Empty;

    [BsonElement("summary")]
    [BsonIgnoreIfDefault]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("fullDescription")]
    [BsonIgnoreIfDefault]
    public string FullDescription { get; set; } = string.Empty;

    [BsonElement("department")]
    [BsonIgnoreIfDefault]
    public string Department { get; set; } = string.Empty;

    [BsonElement("url")]
    [BsonIgnoreIfDefault]
    public string Url { get; set; } = string.Empty;

    [BsonElement("datePosted")]
    public DateTime DatePosted { get; set; }

    [BsonElement("postedDate")]
    [BsonIgnoreIfDefault]
    public DateTime? PostedDate { get; set; } // Nullable version for better handling

    [BsonElement("scrapedAt")]
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("isRemote")]
    public bool IsRemote { get; set; }

    [BsonElement("jobType")]
    public string JobType { get; set; } = string.Empty; // Full-time, Contract, etc.

    [BsonElement("experienceLevel")]
    [BsonIgnoreIfDefault]
    public string ExperienceLevel { get; set; } = string.Empty;

    [BsonElement("requiredSkills")]
    [BsonIgnoreIfDefault]
    public List<string> RequiredSkills { get; set; } = [];

    [BsonElement("preferredSkills")]
    [BsonIgnoreIfDefault]
    public List<string> PreferredSkills { get; set; } = [];

    [BsonElement("technologies")]
    [BsonIgnoreIfDefault]
    public List<string> Technologies { get; set; } = [];

    [BsonElement("requirements")]
    [BsonIgnoreIfDefault]
    public List<string> Requirements { get; set; } = []; // For market intelligence analysis

    [BsonElement("benefits")]
    [BsonIgnoreIfDefault]
    public string Benefits { get; set; } = string.Empty;

    [BsonElement("sourceSite")]
    public JobSite SourceSite { get; set; }

    [BsonElement("matchScore")]
    public double MatchScore { get; set; } // Against user profile

    [BsonElement("isApplied")]
    public bool IsApplied { get; set; }

    [BsonElement("notes")]
    [BsonIgnoreIfDefault]
    public string Notes { get; set; } = string.Empty;

    // Phase 2 Enhancement Fields
    public DuplicateJobInfo? DuplicateInfo { get; set; } // Smart deduplication data
    public ApplicationCategoryInfo? ApplicationCategory { get; set; } // Application management data
    public MarketIntelligenceData? MarketData { get; set; } // Market intelligence data

    // Legacy fields from older schema - ignore if not present
    [BsonElement("source")]
    [BsonIgnoreIfDefault]
    public string source { get; set; } = string.Empty;

    // Additional fields from JobSpy integration and MongoDB schema
    [BsonElement("searchTerm")]
    [BsonIgnoreIfDefault]
    public string searchTerm { get; set; } = string.Empty; // Original search term

    [BsonElement("searchLocation")]
    [BsonIgnoreIfDefault]
    public string searchLocation { get; set; } = string.Empty; // Original search location

    [BsonElement("status")]
    [BsonIgnoreIfDefault]
    public string status { get; set; } = string.Empty; // Application status (e.g., "new")

    [BsonElement("salaryMin")]
    [BsonIgnoreIfDefault]
    public decimal? salaryMin { get; set; } // Minimum salary

    [BsonElement("salaryMax")]
    [BsonIgnoreIfDefault]
    public decimal? salaryMax { get; set; } // Maximum salary

    [BsonElement("salaryInterval")]
    [BsonIgnoreIfDefault]
    public string salaryInterval { get; set; } = string.Empty; // e.g., "yearly", "hourly"

    [BsonElement("currency")]
    [BsonIgnoreIfDefault]
    public string currency { get; set; } = string.Empty; // e.g., "USD"

    [BsonElement("companyUrl")]
    [BsonIgnoreIfDefault]
    public string companyUrl { get; set; } = string.Empty; // Company profile URL

    [BsonElement("jobFunction")]
    [BsonIgnoreIfDefault]
    public string jobFunction { get; set; } = string.Empty; // Job function category

    [BsonElement("companyIndustry")]
    [BsonIgnoreIfDefault]
    public string companyIndustry { get; set; } = string.Empty; // Company industry

    [BsonElement("jobLevel")]
    [BsonIgnoreIfDefault]
    public string jobLevel { get; set; } = string.Empty; // Job seniority level

    [BsonElement("createdAt")]
    [BsonIgnoreIfDefault]
    public DateTime? createdAt { get; set; } // Record creation timestamp

    [BsonElement("lastUpdated")]
    [BsonIgnoreIfDefault]
    public DateTime? lastUpdated { get; set; } // Last update timestamp

    // Aggregation fields from search sessions
    [BsonElement("total_searches")]
    [BsonIgnoreIfDefault]
    public int total_searches { get; set; }

    [BsonElement("successful_searches")]
    [BsonIgnoreIfDefault]
    public int successful_searches { get; set; }

    [BsonElement("total_jobs_found")]
    [BsonIgnoreIfDefault]
    public int total_jobs_found { get; set; }

    [BsonElement("high_scoring_jobs")]
    [BsonIgnoreIfDefault]
    public int high_scoring_jobs { get; set; }

    [BsonElement("job_boards_used")]
    [BsonIgnoreIfDefault]
    public List<string> job_boards_used { get; set; } = [];

    [BsonElement("search_terms_used")]
    [BsonIgnoreIfDefault]
    public List<string> search_terms_used { get; set; } = [];

    [BsonElement("session_timestamp")]
    [BsonIgnoreIfDefault]
    public DateTime? session_timestamp { get; set; }
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

/// <summary>
/// Temporary job listing for incremental saving during scraping operations
/// Allows recovery if job is aborted, AI times out, or errors occur
/// </summary>
public class TemporaryJobListing
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty; // Groups related results (jobId from async queue or generated)

    [BsonElement("batchNumber")]
    public int BatchNumber { get; set; } // Track batch order

    [BsonElement("savedAt")]
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("consolidated")]
    public bool Consolidated { get; set; } = false; // Marks if moved to final collection

    [BsonElement("jobListing")]
    public EnhancedJobListing JobListing { get; set; } = new(); // The actual job data

    [BsonElement("operationType")]
    public string OperationType { get; set; } = string.Empty; // "bulk", "single_site", "multi_site"

    [BsonElement("searchTerm")]
    public string SearchTerm { get; set; } = string.Empty; // For context

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty; // For context
}