namespace PlaywrightServerMcp.Models;

/// <summary>
/// Testing environment information
/// </summary>
public class TestingEnvironmentInfo
{
    public bool AngularDetected { get; set; }
    public string AngularVersion { get; set; } = string.Empty;
    public bool DevToolsAvailable { get; set; }
    public bool ComponentTestingSupported { get; set; }
    public bool SignalsSupported { get; set; }
    public bool StandaloneComponentsSupported { get; set; }
    public string TestingFramework { get; set; } = string.Empty;
    public List<string> AvailableTestingLibraries { get; set; } = [];
}