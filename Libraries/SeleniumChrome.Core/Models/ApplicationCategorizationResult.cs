namespace SeleniumChrome.Core.Models;

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