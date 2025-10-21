namespace PlaywrightServerMcp.Models;

/// <summary>
/// Code coverage report
/// </summary>
public class CoverageReport
{
    public bool CoverageEnabled { get; set; }
    public double LineCoverage { get; set; }
    public double BranchCoverage { get; set; }
    public double FunctionCoverage { get; set; }
    public double StatementCoverage { get; set; }
    public List<FileCoverage> FileCoverages { get; set; } = [];
    public string CoverageReportPath { get; set; } = string.Empty;
    public CoverageThresholds Thresholds { get; set; } = new();
}