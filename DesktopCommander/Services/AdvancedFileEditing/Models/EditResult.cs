namespace DesktopCommander.Services.AdvancedFileEditing.Models;

public class EditResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public string? DiffPreview { get; set; }
    public int LinesAffected { get; set; }
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Version token after the edit was applied.
    /// Only present when edit is actually applied (not in preview mode).
    /// </summary>
    public string? NewVersionToken { get; set; }
    
    /// <summary>
    /// Approval token for the pending edit.
    /// Only present in preview mode - required to apply the edit.
    /// </summary>
    public string? ApprovalToken { get; set; }
    
    /// <summary>
    /// Full file content preview with edits applied.
    /// Only present in preview mode.
    /// </summary>
    public string? PreviewContent { get; set; }
    
    /// <summary>
    /// When the approval token expires (if in preview mode)
    /// </summary>
    public DateTime? ApprovalExpiresAt { get; set; }
    
    /// <summary>
    /// Indicates whether this is a preview (pending approval) or applied edit
    /// </summary>
    public bool IsPreview { get; set; }
    
    /// <summary>
    /// Creates a successful preview result (edit not yet applied)
    /// </summary>
    public static EditResult CreatePreview(
        string filePath, 
        int linesAffected, 
        string message,
        string approvalToken,
        DateTime expiresAt,
        string previewContent,
        string? diffPreview = null)
    {
        return new EditResult
        {
            Success = true,
            IsPreview = true,
            FilePath = filePath,
            LinesAffected = linesAffected,
            Message = message,
            ApprovalToken = approvalToken,
            ApprovalExpiresAt = expiresAt,
            PreviewContent = previewContent,
            DiffPreview = diffPreview
        };
    }
    
    /// <summary>
    /// Creates a successful applied edit result
    /// NOTE: Does NOT include version token to force file re-read before next edit
    /// </summary>
    public static EditResult CreateSuccess(
        string filePath, 
        int linesAffected, 
        string message, 
        string? diffPreview = null)
    {
        return new EditResult
        {
            Success = true,
            IsPreview = false,
            FilePath = filePath,
            LinesAffected = linesAffected,
            Message = message,
            NewVersionToken = null,
            DiffPreview = diffPreview
        };
    }
    
    /// <summary>
    /// Creates a failed edit result
    /// </summary>
    public static EditResult CreateFailure(string filePath, string message, string? errorDetails = null)
    {
        return new EditResult
        {
            Success = false,
            IsPreview = false,
            FilePath = filePath,
            Message = message,
            ErrorDetails = errorDetails,
            LinesAffected = 0
        };
    }
    
    /// <summary>
    /// Formats the result as a user-friendly string for MCP responses
    /// </summary>
    public string FormatForUser()
    {
        if (!Success)
        {
            var errorResult = $"❌ {Message}\n";
            errorResult += $"File: {FilePath}";
            
            if (!string.IsNullOrEmpty(ErrorDetails))
            {
                errorResult += $"\nDetails: {ErrorDetails}";
            }
            
            return errorResult;
        }
        
        if (IsPreview)
        {
            var result = "📋 PREVIEW - Edit NOT Yet Applied\n";
            result += $"✅ {Message}\n";
            result += $"File: {FilePath}\n";
            result += $"Lines affected: {LinesAffected}\n";
            result += $"\n⏰ Approval expires at: {ApprovalExpiresAt:yyyy-MM-dd HH:mm:ss} UTC";
            result += $"\n\n🔐 Approval Token: {ApprovalToken}";
            result += "\n\n⚠️ CRITICAL: You MUST approve this edit before it will be applied!";
            result += "\n   Use the approve_file_edit tool with this approval token.";
            result += "\n\n📄 FULL FILE PREVIEW (with edits applied):";
            result += $"\n{new string('=', 80)}\n";
            result += PreviewContent;
            result += $"\n{new string('=', 80)}";
            
            if (!string.IsNullOrEmpty(DiffPreview))
            {
                result += $"\n\n📊 CHANGES SUMMARY:\n{DiffPreview}";
            }
            
            return result;
        }
        else
        {
            var result = "✅ Edit Applied Successfully\n";
            result += $"{Message}\n";
            result += $"File: {FilePath}\n";
            result += $"Lines affected: {LinesAffected}";
            
            result += "\n\n⚠️ IMPORTANT: You must re-read the file before making another edit.";
            result += "\n   Use read_file or advanced_file_read_range to:";
            result += "\n   1. Verify the edit was applied correctly";
            result += "\n   2. Get the current version token for your next edit";
            
            if (!string.IsNullOrEmpty(DiffPreview))
            {
                result += $"\n\n📊 Changes applied:\n{DiffPreview}";
            }
            
            return result;
        }
    }
}