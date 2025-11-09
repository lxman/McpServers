namespace SeleniumChrome.Core.Models;

public class ApplicationInsights
{
    public DateTime GeneratedAt { get; set; }
    public List<CategorizedJob> DailyApplicationPlan { get; set; } = [];
    public List<CategorizedJob> WeeklyApplicationPlan { get; set; } = [];
    public TimeSpan EstimatedDailyTime { get; set; }
    public TimeSpan EstimatedWeeklyTime { get; set; }
    public List<string> Recommendations { get; set; } = [];
}