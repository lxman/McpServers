namespace DesktopDriver.Services.AdvancedFileEditing.Models;

public class EditResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
    public string? DiffPreview { get; set; }
    public int LinesAffected { get; set; }
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Creates a successful edit result
    /// </summary>
    public static EditResult CreateSuccess(string filePath, int linesAffected, string message, string? diffPreview = null)
    {
        return new EditResult
        {
            Success = true,
            FilePath = filePath,
            LinesAffected = linesAffected,
            Message = message,
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
        if (Success)
        {
            var result = $"✅ {Message}\n";
            result += $"File: {FilePath}\n";
            result += $"Lines affected: {LinesAffected}";
            
            if (!string.IsNullOrEmpty(DiffPreview))
            {
                result += $"\n\nPreview of changes:\n{DiffPreview}";
            }
            
            return result;
        }
        else
        {
            var result = $"❌ {Message}\n";
            result += $"File: {FilePath}";
            
            if (!string.IsNullOrEmpty(ErrorDetails))
            {
                result += $"\nDetails: {ErrorDetails}";
            }
            
            return result;
        }
    }
}