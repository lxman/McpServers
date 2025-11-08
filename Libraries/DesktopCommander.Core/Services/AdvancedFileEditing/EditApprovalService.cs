using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using DesktopCommander.Core.Services.AdvancedFileEditing.Models;

namespace DesktopCommander.Core.Services.AdvancedFileEditing;

/// <summary>
/// Manages pending edit operations that require approval before being applied
/// </summary>
public class EditApprovalService
{
    private readonly ConcurrentDictionary<string, PendingEdit> _pendingEdits = new();
    private readonly TimeSpan _defaultExpirationTime = TimeSpan.FromMinutes(5);
    private readonly Timer _cleanupTimer;
    
    public EditApprovalService()
    {
        // Cleanup expired edits every minute
        _cleanupTimer = new Timer(CleanupExpiredEdits, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }
    
    /// <summary>
    /// Creates a new pending edit and returns an approval token
    /// </summary>
    public PendingEdit CreatePendingEdit(
        string filePath, 
        EditOperation operation,
        string originalVersionToken,
        string previewContent,
        string diffPreview,
        int linesAffected,
        bool createBackup,
        Dictionary<string, object>? metadata = null)
    {
        var approvalToken = GenerateApprovalToken();
        
        var pendingEdit = new PendingEdit
        {
            ApprovalToken = approvalToken,
            FilePath = filePath,
            Operation = operation,
            OriginalVersionToken = originalVersionToken,
            PreviewContent = previewContent,
            DiffPreview = diffPreview,
            LinesAffected = linesAffected,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_defaultExpirationTime),
            CreateBackup = createBackup,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
        
        if (!_pendingEdits.TryAdd(approvalToken, pendingEdit))
        {
            throw new InvalidOperationException("Failed to create pending edit - token collision");
        }
        
        return pendingEdit;
    }
    
    /// <summary>
    /// Retrieves a pending edit by approval token and removes it from the pending list
    /// </summary>
    public PendingEdit? ConsumePendingEdit(string approvalToken)
    {
        if (string.IsNullOrWhiteSpace(approvalToken))
            return null;
            
        if (!_pendingEdits.TryRemove(approvalToken, out var pendingEdit))
            return null;
            
        // Check if expired
        if (pendingEdit.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }
        
        return pendingEdit;
    }
    
    /// <summary>
    /// Gets pending edits for a specific file
    /// </summary>
    public IReadOnlyList<PendingEdit> GetPendingEditsForFile(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        
        return _pendingEdits.Values
            .Where(pe => Path.GetFullPath(pe.FilePath) == normalizedPath && pe.ExpiresAt > DateTime.UtcNow)
            .OrderBy(pe => pe.CreatedAt)
            .ToList();
    }
    
    /// <summary>
    /// Gets all active pending edits (not expired)
    /// </summary>
    public IReadOnlyList<PendingEdit> GetAllPendingEdits()
    {
        return _pendingEdits.Values
            .Where(pe => pe.ExpiresAt > DateTime.UtcNow)
            .OrderBy(pe => pe.CreatedAt)
            .ToList();
    }
    
    /// <summary>
    /// Cancels a pending edit
    /// </summary>
    public bool CancelPendingEdit(string approvalToken)
    {
        return _pendingEdits.TryRemove(approvalToken, out _);
    }
    
    /// <summary>
    /// Cancels all pending edits for a file
    /// </summary>
    public int CancelPendingEditsForFile(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var tokensToRemove = _pendingEdits.Values
            .Where(pe => Path.GetFullPath(pe.FilePath) == normalizedPath)
            .Select(pe => pe.ApprovalToken)
            .ToList();

        return tokensToRemove.Count(token => _pendingEdits.TryRemove(token, out _));
    }
    
    private void CleanupExpiredEdits(object? state)
    {
        var expiredTokens = _pendingEdits.Values
            .Where(pe => pe.ExpiresAt < DateTime.UtcNow)
            .Select(pe => pe.ApprovalToken)
            .ToList();
        
        foreach (var token in expiredTokens)
        {
            _pendingEdits.TryRemove(token, out _);
        }
    }
    
    private static string GenerateApprovalToken()
    {
        // Generate a cryptographically secure random token
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        
        var tokenBase = Convert.ToBase64String(randomBytes);
        var timestamp = DateTime.UtcNow.Ticks.ToString();
        
        // Hash for additional security and consistent length
        var combinedBytes = Encoding.UTF8.GetBytes($"{tokenBase}:{timestamp}");
        var hashBytes = SHA256.HashData(combinedBytes);
        
        return $"approval_{Convert.ToHexStringLower(hashBytes)}";
    }
}