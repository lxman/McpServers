using MongoDB.Driver;
using SeleniumChromeTool.Models;

namespace SeleniumChromeTool.Services.Enhanced;

/// <summary>
/// Phase 2 Enhancement: Application Management Service
/// Provides intelligent application tracking and categorization
/// </summary>
public class ApplicationManagementService
{
    private readonly ILogger<ApplicationManagementService> _logger;
    private readonly IMongoCollection<ApplicationRecord>? _applicationCollection;

    public ApplicationManagementService(
        ILogger<ApplicationManagementService> logger,
        IMongoDatabase? database = null)
    {
        _logger = logger;
        
        if (database != null)
        {
            _applicationCollection = database.GetCollection<ApplicationRecord>("job_applications");
        }
    }

    /// <summary>
    /// Categorize jobs based on application readiness and user preferences
    /// </summary>
    public async Task<ApplicationCategorizationResult> CategorizeJobsAsync(
        List<EnhancedJobListing> jobs, 
        ApplicationPreferences? preferences = null)
    {
        _logger.LogInformation($"Categorizing {jobs.Count} jobs for application readiness");

        // Provide default preferences if none provided
        if (preferences == null)
        {
            preferences = new ApplicationPreferences
            {
                UserId = "default_user",
                MinSalary = 80000,
                PreferredSalary = 120000,
                PreferRemote = true,
                TargetExperienceLevel = ExperienceLevel.Senior,
                DailyApplicationTarget = 3,
                WeeklyApplicationTarget = 15
            };
        }

        var result = new ApplicationCategorizationResult
        {
            ProcessedAt = DateTime.UtcNow,
            TotalJobs = jobs.Count
        };

        // Get existing applications to avoid duplicates
        List<ApplicationRecord> existingApplications = await GetExistingApplicationsAsync(preferences.UserId);
        HashSet<string> appliedJobUrls = existingApplications.Select(a => a.JobUrl).ToHashSet();

        foreach (EnhancedJobListing job in jobs)
        {
            CategorizedJob category = await CategorizeJobAsync(job, preferences, appliedJobUrls);
            
            switch (category.Priority)
            {
                case ApplicationPriority.Immediate:
                    result.ImmediateApplications.Add(category);
                    break;
                case ApplicationPriority.High:
                    result.HighPriorityApplications.Add(category);
                    break;
                case ApplicationPriority.Medium:
                    result.MediumPriorityApplications.Add(category);
                    break;
                case ApplicationPriority.Low:
                    result.LowPriorityApplications.Add(category);
                    break;
                case ApplicationPriority.NotRecommended:
                    result.NotRecommended.Add(category);
                    break;
                case ApplicationPriority.AlreadyApplied:
                    result.AlreadyApplied.Add(category);
                    break;
            }
        }

        // Calculate insights
        result.Insights = await GenerateApplicationInsightsAsync(result, preferences);

        _logger.LogInformation($"Categorization complete: {result.ImmediateApplications.Count} immediate, " +
                             $"{result.HighPriorityApplications.Count} high priority, " +
                             $"{result.AlreadyApplied.Count} already applied");

        return result;
    }

    /// <summary>
    /// Categorize a single job for application readiness
    /// </summary>
    private async Task<CategorizedJob> CategorizeJobAsync(
        EnhancedJobListing job, 
        ApplicationPreferences preferences, 
        HashSet<string> appliedJobUrls)
    {
        var categorized = new CategorizedJob
        {
            Job = job,
            AnalyzedAt = DateTime.UtcNow
        };

        // Check if already applied
        if (appliedJobUrls.Contains(job.JobUrl) || 
            await HasSimilarApplicationAsync(job, preferences.UserId))
        {
            categorized.Priority = ApplicationPriority.AlreadyApplied;
            categorized.ReasonCodes.Add("ALREADY_APPLIED");
            return categorized;
        }

        // Calculate application readiness score
        int score = CalculateApplicationReadinessScore(job, preferences);
        categorized.ApplicationReadinessScore = score;

        // Determine priority based on score and additional factors
        categorized.Priority = DeterminePriority(job, score, preferences);
        categorized.ReasonCodes = GenerateReasonCodes(job, score, preferences);
        categorized.ActionItems = GenerateActionItems(job, preferences);
        categorized.EstimatedApplicationTime = EstimateApplicationTime(job, preferences);
        categorized.DeadlineUrgency = CalculateDeadlineUrgency(job);
        categorized.CompetitivenessRating = CalculateCompetitiveness(job, preferences);

        return categorized;
    }

    /// <summary>
    /// Calculate how ready a job is for immediate application
    /// </summary>
    private int CalculateApplicationReadinessScore(EnhancedJobListing job, ApplicationPreferences preferences)
    {
        var score = 0;

        // Match score heavily influences readiness
        score += (int)(job.MatchScore * 0.4); // Max 40 points

        // Salary alignment
        if (!string.IsNullOrEmpty(job.Salary))
        {
            int salaryScore = CalculateSalaryAlignment(job.Salary, preferences);
            score += salaryScore; // Max 15 points
        }

        // Location preference
        if (job.IsRemote && preferences.PreferRemote)
        {
            score += 15;
        }
        else if (IsLocationPreferred(job.Location, preferences.PreferredLocations))
        {
            score += 10;
        }

        // Company factors
        if (IsTargetCompany(job.Company, preferences.TargetCompanies))
        {
            score += 15;
        }

        // Technology stack alignment
        int techScore = CalculateTechnologyAlignment(job, preferences);
        score += techScore; // Max 10 points

        // Urgency factors
        if (HasApplicationDeadline(job))
        {
            score += 5;
        }

        // Experience level match
        if (IsExperienceLevelMatch(job, preferences))
        {
            score += 5;
        }

        return Math.Min(score, 100); // Cap at 100
    }

    // Helper methods with simplified implementations
    private int CalculateSalaryAlignment(string jobSalary, ApplicationPreferences preferences) => 10;
    private bool IsLocationPreferred(string jobLocation, List<string> preferredLocations) => false;
    private bool IsTargetCompany(string company, List<string> targetCompanies) => false;
    private int CalculateTechnologyAlignment(EnhancedJobListing job, ApplicationPreferences preferences) => 5;
    private bool HasApplicationDeadline(EnhancedJobListing job) => false;
    private bool IsExperienceLevelMatch(EnhancedJobListing job, ApplicationPreferences preferences) => true;

    private ApplicationPriority DeterminePriority(EnhancedJobListing job, int score, ApplicationPreferences preferences)
    {
        if (score >= 85) return ApplicationPriority.Immediate;
        if (score >= 70) return ApplicationPriority.High;
        if (score >= 50) return ApplicationPriority.Medium;
        if (score >= 30) return ApplicationPriority.Low;
        return ApplicationPriority.NotRecommended;
    }

    private List<string> GenerateReasonCodes(EnhancedJobListing job, int score, ApplicationPreferences preferences) =>
        ["AUTOMATED_ANALYSIS"];

    private List<string> GenerateActionItems(EnhancedJobListing job, ApplicationPreferences preferences) =>
        ["Review job requirements", "Customize resume", "Prepare cover letter"];

    private TimeSpan EstimateApplicationTime(EnhancedJobListing job, ApplicationPreferences preferences) =>
        TimeSpan.FromMinutes(45);

    private UrgencyLevel CalculateDeadlineUrgency(EnhancedJobListing job) => UrgencyLevel.Normal;

    private CompetitivenessLevel CalculateCompetitiveness(EnhancedJobListing job, ApplicationPreferences preferences) =>
        CompetitivenessLevel.Medium;

    private async Task<List<ApplicationRecord>> GetExistingApplicationsAsync(string userId) =>
        [];

    private async Task<bool> HasSimilarApplicationAsync(EnhancedJobListing job, string userId) => false;

    private async Task<ApplicationInsights> GenerateApplicationInsightsAsync(
        ApplicationCategorizationResult result, 
        ApplicationPreferences preferences)
    {
        return new ApplicationInsights
        {
            GeneratedAt = DateTime.UtcNow,
            DailyApplicationPlan = result.ImmediateApplications.Take(3).ToList(),
            WeeklyApplicationPlan = result.ImmediateApplications.Concat(result.HighPriorityApplications).Take(15).ToList(),
            EstimatedDailyTime = TimeSpan.FromHours(2),
            EstimatedWeeklyTime = TimeSpan.FromHours(10),
            Recommendations = ["Focus on high-priority applications first"]
        };
    }

    /// <summary>
    /// Track job application submission
    /// </summary>
    public async Task<bool> TrackApplicationAsync(ApplicationRecord application)
    {
        if (_applicationCollection == null) return false;

        try
        {
            application.AppliedAt = DateTime.UtcNow;
            application.Status = ApplicationStatus.Applied;
            
            await _applicationCollection.InsertOneAsync(application);
            
            _logger.LogInformation($"Tracked application: {application.Company} - {application.Title}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to track application: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update application status (interview, rejection, etc.)
    /// </summary>
    public async Task<bool> UpdateApplicationStatusAsync(string applicationId, ApplicationStatus status, string? notes = null)
    {
        if (_applicationCollection == null) return false;

        try
        {
            FilterDefinition<ApplicationRecord>? filter = Builders<ApplicationRecord>.Filter.Eq(a => a.Id, applicationId);
            UpdateDefinition<ApplicationRecord>? update = Builders<ApplicationRecord>.Update
                .Set(a => a.Status, status)
                .Set(a => a.LastUpdated, DateTime.UtcNow);
            
            if (!string.IsNullOrEmpty(notes))
            {
                update = update.Set(a => a.Notes, notes);
            }

            UpdateResult? result = await _applicationCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update application status: {ex.Message}");
            return false;
        }
    }
}

// Supporting Models and Enums

public class ApplicationCategorizationResult
{
    public DateTime ProcessedAt { get; set; }
    public int TotalJobs { get; set; }
    public List<CategorizedJob> ImmediateApplications { get; set; } = [];
    public List<CategorizedJob> HighPriorityApplications { get; set; } = [];
    public List<CategorizedJob> MediumPriorityApplications { get; set; } = [];
    public List<CategorizedJob> LowPriorityApplications { get; set; } = [];
    public List<CategorizedJob> NotRecommended { get; set; } = [];
    public List<CategorizedJob> AlreadyApplied { get; set; } = [];
    public ApplicationInsights Insights { get; set; } = new();
}

public class CategorizedJob
{
    public EnhancedJobListing Job { get; set; } = null!;
    public ApplicationPriority Priority { get; set; }
    public int ApplicationReadinessScore { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
    public List<string> ActionItems { get; set; } = [];
    public TimeSpan EstimatedApplicationTime { get; set; }
    public UrgencyLevel DeadlineUrgency { get; set; }
    public CompetitivenessLevel CompetitivenessRating { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

public class ApplicationPreferences
{
    public string UserId { get; set; } = string.Empty;
    public decimal MinSalary { get; set; }
    public decimal PreferredSalary { get; set; }
    public bool PreferRemote { get; set; }
    public List<string> PreferredLocations { get; set; } = [];
    public List<string> TargetCompanies { get; set; } = [];
    public List<string> RequiredTechnologies { get; set; } = [];
    public ExperienceLevel TargetExperienceLevel { get; set; }
    public int DailyApplicationTarget { get; set; } = 3;
    public int WeeklyApplicationTarget { get; set; } = 15;
}

public class ApplicationInsights
{
    public DateTime GeneratedAt { get; set; }
    public List<CategorizedJob> DailyApplicationPlan { get; set; } = [];
    public List<CategorizedJob> WeeklyApplicationPlan { get; set; } = [];
    public TimeSpan EstimatedDailyTime { get; set; }
    public TimeSpan EstimatedWeeklyTime { get; set; }
    public List<string> Recommendations { get; set; } = [];
}

public class ApplicationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string JobUrl { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Salary { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<string> FollowUpDates { get; set; } = [];
}

public enum ApplicationPriority
{
    Immediate,
    High,
    Medium,
    Low,
    NotRecommended,
    AlreadyApplied
}

public enum ApplicationStatus
{
    Planned,
    Applied,
    UnderReview,
    Interview,
    Rejected,
    Offered,
    Accepted,
    Withdrawn
}

public enum ExperienceLevel
{
    Junior,
    Mid,
    Senior,
    Lead,
    Principal,
    NotSpecified
}

public enum UrgencyLevel
{
    Normal,
    Medium,
    High,
    Critical
}

public enum CompetitivenessLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}

public enum HiringVelocity
{
    Low,
    Medium,
    High,
    VeryHigh
}
