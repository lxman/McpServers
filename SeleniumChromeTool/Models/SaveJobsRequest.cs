namespace SeleniumChromeTool.Models;

public class SaveJobsRequest
{
    public string UserId { get; set; } = string.Empty;
    public List<EnhancedJobListing> Jobs { get; set; } = [];
    public bool OverwriteExisting { get; set; } = false;
}