namespace PlaywrightServerMcp.Models;

/// <summary>
/// Test environment information
/// </summary>
public class TestEnvironmentInfo
{
    public string NodeVersion { get; set; } = string.Empty;
    public string AngularVersion { get; set; } = string.Empty;
    public string TestFrameworkVersion { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public bool ChromeInstalled { get; set; }
    public bool FirefoxInstalled { get; set; }
}