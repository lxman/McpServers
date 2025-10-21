namespace PlaywrightServerMcp.Models;

/// <summary>
/// Overall contract compliance scoring
/// </summary>
public class ContractComplianceScore
{
    public double OverallScore { get; set; }
    public double InputComplianceScore { get; set; }
    public double OutputComplianceScore { get; set; }
    public double InterfaceComplianceScore { get; set; }
    public double TypeSafetyScore { get; set; }
    public double ErrorHandlingScore { get; set; }
    public double PerformanceScore { get; set; }
    public string ComplianceLevel => OverallScore switch
    {
        >= 90 => "Excellent",
        >= 80 => "Good",
        >= 70 => "Satisfactory",
        >= 60 => "Needs Improvement",
        _ => "Poor"
    };
}