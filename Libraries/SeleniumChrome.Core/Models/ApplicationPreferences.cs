namespace SeleniumChrome.Core.Models;

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