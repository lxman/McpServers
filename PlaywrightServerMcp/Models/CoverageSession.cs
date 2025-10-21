namespace PlaywrightServerMcp.Models;

public class CoverageSession
{
    public string SessionId { get; set; } = "";
    public DateTime StartTime { get; set; }
    public bool IsActive { get; set; } = true;
    public bool JsCoverage { get; set; } = true;
    public bool CssCoverage { get; set; } = true;
    public List<CoverageEntry> Entries { get; set; } = [];
}