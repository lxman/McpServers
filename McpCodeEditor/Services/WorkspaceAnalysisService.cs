namespace McpCodeEditor.Services;

/// <summary>
/// RS-001: Service for workspace analysis operations
/// Extracted from WorkspaceTools.cs to follow Single Responsibility Principle
/// </summary>
public class WorkspaceAnalysisService(ProjectDetectionService projectDetection)
{
    /// <summary>
    /// Analyzes a workspace directory to determine project type and characteristics
    /// </summary>
    /// <param name="workspacePath">Path to the workspace to analyze</param>
    /// <returns>Workspace analysis result or null if analysis fails</returns>
    public async Task<object?> AnalyzeWorkspaceAsync(string workspacePath)
    {
        try
        {
            var projectInfo = await projectDetection.AnalyzeDirectoryAsync(workspacePath);
            return new
            {
                project_type = projectInfo.Type.ToString(),
                project_name = projectInfo.Name,
                description = projectInfo.Description,
                indicators = projectInfo.Indicators,
                score = projectInfo.Score
            };
        }
        catch
        {
            return null;
        }
    }
}
