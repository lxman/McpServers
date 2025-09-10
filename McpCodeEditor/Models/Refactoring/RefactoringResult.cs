namespace McpCodeEditor.Models.Refactoring;

public class RefactoringResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public List<FileChange> Changes { get; set; } = [];
    public int FilesAffected { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a successful refactoring result
    /// </summary>
    public static RefactoringResult CreateSuccess(string message = "")
    {
        return new RefactoringResult
        {
            Success = true,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failed refactoring result
    /// </summary>
    public static RefactoringResult CreateFailure(string error, string message = "")
    {
        return new RefactoringResult
        {
            Success = false,
            Error = error,
            Message = message
        };
    }
}
