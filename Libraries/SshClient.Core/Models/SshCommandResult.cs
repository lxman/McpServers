namespace SshClient.Core.Models;

/// <summary>
/// Result of an SSH command execution
/// </summary>
public record SshCommandResult
{
    /// <summary>
    /// Whether the command executed successfully (exit code 0)
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Exit code of the command
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Standard output from the command
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// Standard error from the command
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// Command that was executed
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// Duration of command execution
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Connection name used for execution
    /// </summary>
    public string ConnectionName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the output was truncated due to size limits
    /// </summary>
    public bool OutputTruncated { get; init; }

    /// <summary>
    /// Original output length if truncated
    /// </summary>
    public int? OriginalOutputLength { get; init; }
}
