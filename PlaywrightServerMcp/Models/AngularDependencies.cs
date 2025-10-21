namespace PlaywrightServerMcp.Models;

/// <summary>
/// Angular-specific dependencies
/// </summary>
public class AngularDependencies
{
    public string CoreVersion { get; set; } = string.Empty;
    public string CliVersion { get; set; } = string.Empty;
    public string TypeScriptVersion { get; set; } = string.Empty;
    public string RxJsVersion { get; set; } = string.Empty;
    public List<string> AngularPackages { get; set; } = [];
    public bool StandaloneSupport { get; set; }
    public bool SignalsSupport { get; set; }
    public bool ZonelessSupport { get; set; }
}