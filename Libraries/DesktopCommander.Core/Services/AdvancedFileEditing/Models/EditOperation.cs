namespace DesktopCommander.Core.Services.AdvancedFileEditing.Models;

public class EditOperation
{
    public EditOperationType Type { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Creates a replace operation for the specified line range
    /// </summary>
    public static EditOperation Replace(int startLine, int endLine, string content, string description = "")
    {
        return new EditOperation
        {
            Type = EditOperationType.Replace,
            StartLine = startLine,
            EndLine = endLine,
            Content = content,
            Description = description
        };
    }
    
    /// <summary>
    /// Creates an insert operation after the specified line
    /// </summary>
    public static EditOperation Insert(int afterLine, string content, string description = "")
    {
        return new EditOperation
        {
            Type = EditOperationType.Insert,
            StartLine = afterLine,
            EndLine = afterLine,
            Content = content,
            Description = description
        };
    }
    
    /// <summary>
    /// Creates a delete operation for the specified line range
    /// </summary>
    public static EditOperation Delete(int startLine, int endLine, string description = "")
    {
        return new EditOperation
        {
            Type = EditOperationType.Delete,
            StartLine = startLine,
            EndLine = endLine,
            Content = string.Empty,
            Description = description
        };
    }
}