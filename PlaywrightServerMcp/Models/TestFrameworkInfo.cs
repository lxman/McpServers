namespace PlaywrightServerMcp.Models;

/// <summary>
/// Information about the test framework being used
/// </summary>
public class TestFrameworkInfo
{
    public string Framework { get; set; } = string.Empty; // karma, jest, web-test-runner
    public string TestRunner { get; set; } = string.Empty; // jasmine, mocha, etc.
    public string Browser { get; set; } = string.Empty; // chrome, firefox, etc.
    public bool HeadlessMode { get; set; }
    public bool WatchMode { get; set; }
    public bool CiMode { get; set; }
    public string ConfigFile { get; set; } = string.Empty;
}