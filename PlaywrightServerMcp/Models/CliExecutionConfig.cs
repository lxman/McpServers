namespace PlaywrightServerMcp.Models;

/// <summary>
/// Configuration for Angular CLI command execution
/// </summary>
public class CliExecutionConfig
{
    public string WorkingDirectory { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public bool CaptureOutput { get; set; } = true;
    public bool ValidateAngularProject { get; set; } = true;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}