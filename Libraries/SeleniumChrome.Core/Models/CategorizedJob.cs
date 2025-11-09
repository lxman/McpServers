namespace SeleniumChrome.Core.Models;

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