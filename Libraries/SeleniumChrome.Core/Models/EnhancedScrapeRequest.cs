namespace SeleniumChrome.Core.Models;

public class EnhancedScrapeRequest
{
    public string SearchTerm { get; set; } = ".net developer";
    public string Location { get; set; } = "Remote";
    public int MaxResults { get; set; } = 50;
    public List<JobSite> Sites { get; set; } = [JobSite.Indeed];
    public bool IncludeDescription { get; set; } = false;
    public int MaxAgeInDays { get; set; } = 30;
    public string UserId { get; set; } = string.Empty;
}