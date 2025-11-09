namespace SeleniumChrome.Core.Models;

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