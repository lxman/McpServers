namespace McpCodeEditor.Models;

/// <summary>
/// Represents a research attestation for type verification before file creation.
/// Forces a behavioral pause to verify types before writing code files.
/// </summary>
public class ResearchAttestation
{
    /// <summary>
    /// Unique token for this attestation
    /// </summary>
    public string Token { get; init; } = string.Empty;
    
    /// <summary>
    /// Path to the file that will be created
    /// </summary>
    public string TargetFilePath { get; init; } = string.Empty;
    
    /// <summary>
    /// Types that were researched (for audit trail)
    /// </summary>
    public List<string> TypesResearched { get; init; } = [];
    
    /// <summary>
    /// When this attestation was created
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// When this attestation expires (10 minute window)
    /// </summary>
    public DateTime ExpiresAt { get; init; }
    
    /// <summary>
    /// Check if this attestation is still valid
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
