namespace PlaywrightServerMcp.Models;

/// <summary>
/// Result structure for Angular CLI command execution
/// </summary>
public class CliCommandResult
{
    public bool Success { get; set; }
    public string Command { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool AngularCliDetected { get; set; }
    public string AngularCliVersion { get; set; } = string.Empty;
    public List<string> GeneratedFiles { get; set; } = [];
    public List<string> ModifiedFiles { get; set; } = [];
}