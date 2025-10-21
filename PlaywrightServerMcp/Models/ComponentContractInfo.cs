namespace PlaywrightServerMcp.Models;

/// <summary>
/// Component contract information structure
/// </summary>
public class ComponentContractInfo
{
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentPath { get; set; } = string.Empty;
    public string ComponentSelector { get; set; } = string.Empty;
    public bool IsStandalone { get; set; }
    public List<ComponentInput> Inputs { get; set; } = [];
    public List<ComponentOutput> Outputs { get; set; } = [];
    public List<ComponentMethod> PublicMethods { get; set; } = [];
    public List<ComponentProperty> PublicProperties { get; set; } = [];
    public ChangeDetectionInfo ChangeDetection { get; set; } = new();
    public ComponentLifecycleInfo Lifecycle { get; set; } = new();
}