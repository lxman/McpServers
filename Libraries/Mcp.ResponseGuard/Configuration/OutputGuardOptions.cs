namespace Mcp.ResponseGuard.Configuration;

/// <summary>
/// Configuration options for OutputGuard service
/// </summary>
public class OutputGuardOptions
{
    /// <summary>
    /// Safe token limit accounting for MCP protocol overhead
    /// Default: 20,000 tokens
    /// </summary>
    public int SafeTokenLimit { get; set; } = 20_000;

    /// <summary>
    /// Hard token limit (MCP protocol limit)
    /// Default: 25,000 tokens
    /// </summary>
    public int HardTokenLimit { get; set; } = 25_000;

    /// <summary>
    /// Conservative estimate for characters per token
    /// Default: 4 characters per token
    /// </summary>
    public int CharsPerToken { get; set; } = 4;

    /// <summary>
    /// Whether to automatically truncate oversized responses with a warning
    /// If false, oversized responses will return an error instead
    /// Default: false (return error)
    /// </summary>
    public bool AutoTruncate { get; set; } = false;

    /// <summary>
    /// If AutoTruncate is true, what percentage of safe limit to truncate to
    /// Default: 90% of safe limit
    /// </summary>
    public double TruncateToPercentage { get; set; } = 0.9;

    /// <summary>
    /// Character limits (derived from token limits)
    /// </summary>
    public int SafeCharLimit => SafeTokenLimit * CharsPerToken;

    /// <summary>
    /// Character limits (derived from token limits)
    /// </summary>
    public int HardCharLimit => HardTokenLimit * CharsPerToken;
}
