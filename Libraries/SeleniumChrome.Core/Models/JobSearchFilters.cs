namespace SeleniumChrome.Core.Models;

public class JobSearchFilters
{
    public List<JobSite> Sites { get; set; } = [];
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? IsRemote { get; set; }
    public double? MinMatchScore { get; set; }
    public bool? IsApplied { get; set; }
    public List<string> RequiredSkills { get; set; } = [];
}