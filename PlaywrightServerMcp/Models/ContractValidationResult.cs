namespace PlaywrightServerMcp.Models;

/// <summary>
/// Result structure for component contract validation
/// </summary>
public class ContractValidationResult
{
    public bool Success { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentSelector { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public ComponentContractInfo ContractInfo { get; set; } = new();
    public List<InputValidationResult> InputValidations { get; set; } = [];
    public List<OutputValidationResult> OutputValidations { get; set; } = [];
    public List<InterfaceValidationResult> InterfaceValidations { get; set; } = [];
    public ContractComplianceScore ComplianceScore { get; set; } = new();
    public List<ContractViolation> Violations { get; set; } = [];
    public List<ContractRecommendation> Recommendations { get; set; } = [];
    public TestingEnvironmentInfo Environment { get; set; } = new();
}