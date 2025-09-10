using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services.Enhanced;

// Phase 2 Request Models

public class DeduplicationRequest
{
    public List<EnhancedJobListing> Jobs { get; set; } = new();
}

public class ApplicationCategorizationRequest
{
    public List<EnhancedJobListing> Jobs { get; set; } = new();
    public ApplicationPreferences Preferences { get; set; } = new();
}

public class MarketIntelligenceRequest
{
    public List<EnhancedJobListing> Jobs { get; set; } = new();
    public MarketAnalysisRequest AnalysisRequest { get; set; } = new();
}

public class EnhancedAnalysisRequest
{
    public List<EnhancedJobListing> Jobs { get; set; } = new();
    public string? UserId { get; set; }
    public ApplicationPreferences? ApplicationPreferences { get; set; }
    public MarketAnalysisRequest? MarketAnalysisRequest { get; set; }
}

public class ApplicationTrackingRequest
{
    public ApplicationRecord Application { get; set; } = new();
}

public class ApplicationStatusUpdateRequest
{
    public string ApplicationId { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public string? Notes { get; set; }
}

// Phase 2 Response Models

public class EnhancedAnalysisResult
{
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public int OriginalJobCount { get; set; }
    public List<string> ProcessingSteps { get; set; } = new();
    
    public DeduplicationResult DeduplicationResult { get; set; } = new();
    public ApplicationCategorizationResult CategorizationResult { get; set; } = new();
    public MarketIntelligenceReport MarketIntelligenceResult { get; set; } = new();
}

/// <summary>
/// Request model for comprehensive automated search
/// </summary>
public class ComprehensiveSearchRequest
{
    public string UserId { get; set; } = "automated_search_user";
    public int MaxTotalResults { get; set; } = 50;
    public int JobsPerSearch { get; set; } = 10;
    public int MaxAgeInDays { get; set; } = 30;
    
    public List<string>? CustomSearchTerms { get; set; }
    public List<string>? CustomLocations { get; set; }
    
    public NetDeveloperScoringProfile ScoringProfile { get; set; } = new();
}

/// <summary>
/// Results model for enhanced search with categorized jobs
/// </summary>
public class EnhancedSearchResults
{
    public DateTime SearchStartTime { get; set; }
    public DateTime SearchEndTime { get; set; }
    public TimeSpan TotalSearchDuration { get; set; }
    
    public List<string> SearchTermsUsed { get; set; } = new();
    public int TotalJobsFound { get; set; }
    
    public List<EnhancedJobListing> HighPriorityJobs { get; set; } = new();     // 80%+ match
    public List<EnhancedJobListing> ApplicationReadyJobs { get; set; } = new(); // 60%+ match
    public List<EnhancedJobListing> ConsiderJobs { get; set; } = new();         // 40%+ match
    public List<EnhancedJobListing> SkippedJobs { get; set; } = new();         // Under 40%
    
    public int GetJobsAboveThreshold(int threshold) =>
        HighPriorityJobs.Count + ApplicationReadyJobs.Count + 
        (threshold <= 40 ? ConsiderJobs.Count : 0);
}

/// <summary>
/// Scoring profile for .NET developers based on 50 years experience
/// </summary>
public class NetDeveloperScoringProfile
{
    public SalaryPreferences SalaryPrefs { get; set; } = new();
    public TechnologyPreferences TechPrefs { get; set; } = new();
    public CompanyPreferences CompanyPrefs { get; set; } = new();
    public WorkPreferences WorkPrefs { get; set; } = new();
    public ExperiencePreferences ExperiencePrefs { get; set; } = new();
}

public class SalaryPreferences
{
    public int MinSalary { get; set; } = 120000;
    public int PreferredSalary { get; set; } = 160000;
    public int TargetSalary { get; set; } = 200000;
    public double Weight { get; set; } = 0.25; // 25% of total score
}

public class TechnologyPreferences
{
    public List<string> CoreTechnologies { get; set; } = new() { ".NET", "C#", "ASP.NET Core" };
    public List<string> PreferredTechnologies { get; set; } = new() { "Azure", "Angular", "SQL Server", "Entity Framework" };
    public List<string> BonusTechnologies { get; set; } = new() { "Kubernetes", "Docker", "Microservices", "Redis" };
    public double Weight { get; set; } = 0.30; // 30% of total score
}

public class CompanyPreferences
{
    public List<string> PreferredStages { get; set; } = new() { "Startup", "Scale-up", "Growth" };
    public List<string> AvoidStages { get; set; } = new() { "Enterprise-Legacy" };
    public int MinEmployeeCount { get; set; } = 10;
    public int MaxEmployeeCount { get; set; } = 1000;
    public double Weight { get; set; } = 0.20; // 20% of total score
}

public class WorkPreferences
{
    public bool PreferRemote { get; set; } = true;
    public List<string> AcceptableLocations { get; set; } = new() { "Remote", "Atlanta", "United States" };
    public double Weight { get; set; } = 0.15; // 15% of total score
}

public class ExperiencePreferences
{
    public List<string> TargetLevels { get; set; } = new() { "Senior", "Principal", "Lead", "Staff", "Architect" };
    public List<string> AvoidLevels { get; set; } = new() { "Junior", "Entry", "Intern" };
    public double Weight { get; set; } = 0.10; // 10% of total score
}

/// <summary>
/// Request model for the streamlined SimplifyJobs API service
/// Final solution: Direct API calls with job IDs from web search
/// </summary>
public class FetchJobsByIdsRequest
{
    public string[] JobIds { get; set; } = Array.Empty<string>();
    public string UserId { get; set; } = "default_user";
}

