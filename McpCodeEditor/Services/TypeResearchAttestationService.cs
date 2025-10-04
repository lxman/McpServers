using System.Collections.Concurrent;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Manages type research attestations for code file creation.
/// Enforces a behavioral pause to verify types before writing code.
/// </summary>
public class TypeResearchAttestationService
{
    private readonly ConcurrentDictionary<string, ResearchAttestation> _attestations = new();
    
    /// <summary>
    /// Required attestation text - must match exactly
    /// </summary>
    public const string RequiredAttestationText = 
        "I have thoroughly researched all types and verified property names, method signatures, and constructor parameters";

    /// <summary>
    /// Create a new research attestation after validating the attestation text
    /// </summary>
    public (bool Success, string? Token, string? Error) CreateAttestation(
        string targetFilePath,
        string typesResearched,
        string attestationText)
    {
        // Validate attestation text matches exactly
        if (attestationText != RequiredAttestationText)
        {
            return (false, null, 
                $"Attestation text must match exactly: \"{RequiredAttestationText}\"");
        }

        // Validate we have types to research
        if (string.IsNullOrWhiteSpace(typesResearched))
        {
            return (false, null, "You must list the types you researched (comma-separated)");
        }

        // Generate unique token
        string token = Guid.NewGuid().ToString("N")[..16];
        
        var attestation = new ResearchAttestation
        {
            Token = token,
            TargetFilePath = Path.GetFullPath(targetFilePath),
            TypesResearched = typesResearched
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10) // 10-minute window
        };

        _attestations[token] = attestation;

        // Clean up expired attestations
        CleanupExpiredAttestations();

        return (true, token, null);
    }

    /// <summary>
    /// Validate and consume an attestation token for file creation
    /// </summary>
    public (bool IsValid, string? Error) ValidateAndConsumeToken(string token, string filePath)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "Research token is required for code file creation");
        }

        if (!_attestations.TryGetValue(token, out ResearchAttestation? attestation))
        {
            return (false, "Invalid research token. Please call attest_code_file_research first.");
        }

        if (attestation.IsExpired)
        {
            // Remove expired token
            _attestations.TryRemove(token, out _);
            return (false, "Research token has expired (10-minute limit). Please attest again.");
        }

        string fullPath = Path.GetFullPath(filePath);
        if (!string.Equals(attestation.TargetFilePath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return (false, 
                $"Research token is for different file. Token is for: {attestation.TargetFilePath}, but you're creating: {fullPath}");
        }

        // Consume the token (one-time use)
        _attestations.TryRemove(token, out _);

        return (true, null);
    }

    /// <summary>
    /// Get all active attestations (for debugging/auditing)
    /// </summary>
    public IReadOnlyList<ResearchAttestation> GetActiveAttestations()
    {
        CleanupExpiredAttestations();
        return _attestations.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Remove expired attestations from memory
    /// </summary>
    private void CleanupExpiredAttestations()
    {
        var expired = _attestations
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string key in expired)
        {
            _attestations.TryRemove(key, out _);
        }
    }
}
