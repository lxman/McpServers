namespace McpCodeEditor.Services;

/// <summary>
/// RS-001: Service for workspace information operations
/// Extracted from WorkspaceTools.cs to follow Single Responsibility Principle
/// Handles Git and other workspace information gathering
/// </summary>
public class WorkspaceInfoService(GitService gitService)
{
    /// <summary>
    /// Gets advanced Git information for the current workspace
    /// </summary>
    /// <returns>Git information object or fallback object if not a git repository</returns>
    public async Task<object> GetAdvancedGitInfoAsync()
    {
        try
        {
            // Use the GitService for enhanced Git information
            var gitStatus = await gitService.GetStatusAsync();
            return gitStatus;
        }
        catch
        {
            return new { is_git_repository = false };
        }
    }
}
