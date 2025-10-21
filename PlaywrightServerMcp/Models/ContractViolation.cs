namespace PlaywrightServerMcp.Models;

/// <summary>
/// Contract violation information
/// </summary>
public class ContractViolation
{
    public string ViolationType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Critical, High, Medium, Low
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public List<string> AffectedElements { get; set; } = [];
}