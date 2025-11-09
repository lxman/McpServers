namespace SeleniumChrome.Core.Models;

public class EmailJobAlertSummary
{
    public int TotalJobs { get; set; }
    public int LinkedInJobs { get; set; }
    public int GlassdoorJobs { get; set; }
    public int DiceJobs { get; set; }
    public int IndeedJobs { get; set; }
    public int RemoteJobs { get; set; }
    public int HighMatchJobs { get; set; }
    public int DaysAnalyzed { get; set; }
    public DateTime LastUpdated { get; set; }
}