namespace PlaywrightServerMcp.Models;

/// <summary>
/// Coverage for individual files
/// </summary>
public class FileCoverage
{
    public string FileName { get; set; } = string.Empty;
    public double LineCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double FunctionCoverage { get; set; }
    public double StatementCoverage { get; set; }
    public List<int> UncoveredLines { get; set; } = [];
}