namespace PlaywrightServerMcp.Models;

public class CoverageEntry
{
    public string Url { get; set; } = "";
    public string Type { get; set; } = ""; // "js" or "css"
    public int TotalBytes { get; set; }
    public int UsedBytes { get; set; }
    public double UsagePercentage { get; set; }
    public List<CoverageRange> Ranges { get; set; } = [];
}