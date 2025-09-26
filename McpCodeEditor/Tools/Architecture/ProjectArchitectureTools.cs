using McpCodeEditor.Services;
using McpCodeEditor.Services.Analysis;  // RS-004: Added for RelationshipScoringService
using ProjectInfo = McpCodeEditor.Services.ProjectInfo;
using ModelContextProtocol.Server;
using System.ComponentModel;
using McpCodeEditor.Models;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools.Architecture;

/// <summary>
/// MCP tools for architecture pattern detection and project relationship analysis.
/// Focused responsibility: Architecture detection, project analysis, and workspace suggestions.
/// </summary>
[McpServerToolType]
public class ProjectArchitectureTools(
    CodeEditorConfigurationService config,
    ProjectDetectionService projectDetection,
    ArchitectureDetectionService architectureDetection,
    RelationshipScoringService relationshipScoring,
    ArchitectureRecommendationService architectureRecommendation)
    : BaseToolClass
{
    private readonly ArchitectureRecommendationService _architectureRecommendation = architectureRecommendation;

    #region Architecture Detection MCP Tools

    [McpServerTool]
    [Description("Detect related projects and analyze multi-platform architecture patterns")]
    public async Task<string> DetectRelatedProjectsAsync(
        [Description("Root directory path to analyze for related projects")]
        string rootPath = "")
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            // Use the current workspace if no path provided
            string searchPath = string.IsNullOrEmpty(rootPath) ? config.DefaultWorkspace : rootPath;

            if (!Directory.Exists(searchPath))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {searchPath}");
            }

            // Detect architecture patterns
            List<ArchitecturePattern> patterns = await architectureDetection.DetectArchitecturePatternsAsync(searchPath);

            // Get project information for context
            var projects = new List<ProjectInfo>();
            try
            {
                IEnumerable<string> immediateDirectories = Directory.GetDirectories(searchPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(dir => !IsExcludedDirectory(dir))
                    .Take(10); // Limit for performance

                foreach (string directory in immediateDirectories)
                {
                    ProjectInfo projectInfo = await projectDetection.AnalyzeDirectoryAsync(directory);
                    if (projectInfo.Type != ProjectType.Unknown)
                    {
                        projects.Add(projectInfo);
                    }
                }

                // Also check the root directory
                ProjectInfo rootProject = await projectDetection.AnalyzeDirectoryAsync(searchPath);
                if (rootProject.Type != ProjectType.Unknown)
                {
                    projects.Add(rootProject);
                }
            }
            catch
            {
                // Continue with what we have
            }

            return CreateSuccessResponse(new
            {
                analyzed_path = searchPath,
                total_projects_found = projects.Count,
                total_patterns_detected = patterns.Count,
                projects = projects.Select(p => new
                {
                    path = p.Path,
                    name = p.Name,
                    type = p.Type.ToString(),
                    description = p.Description,
                    indicators = p.Indicators,
                    score = p.Score
                }).ToArray(),
                architecture_patterns = patterns.Select(pattern => new
                {
                    type = pattern.Type.ToString(),
                    name = pattern.Name,
                    description = pattern.Description,
                    confidence_score = pattern.ConfidenceScore,
                    root_path = pattern.RootPath,
                    project_paths = pattern.ProjectPaths,
                    technologies = pattern.Technologies,
                    detection_reasons = pattern.DetectionReasons,
                    indicators_count = pattern.Indicators?.Count ?? 0
                }).ToArray(),
                // RS-004: Using extracted service instead of helper method
                recommendations = ArchitectureRecommendationService.GenerateArchitectureRecommendations(patterns, projects)
            });
        });
    }

    [McpServerTool]
    [Description("Analyze architecture patterns in the current workspace or specified directory")]
    public async Task<string> AnalyzeArchitecturePatternsAsync(
        [Description("Directory path to analyze (uses current workspace if empty)")]
        string? directoryPath = null,
        [Description("Minimum confidence threshold for pattern detection (0.0-1.0)")]
        double minConfidence = 0.6)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            string searchPath = string.IsNullOrEmpty(directoryPath) ? config.DefaultWorkspace : directoryPath;

            if (!Directory.Exists(searchPath))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {searchPath}");
            }

            List<ArchitecturePattern> patterns = await architectureDetection.DetectArchitecturePatternsAsync(searchPath);

            // Filter by confidence threshold
            List<ArchitecturePattern> filteredPatterns = patterns.Where(p => p.ConfidenceScore >= minConfidence).ToList();

            return CreateSuccessResponse(new
            {
                analyzed_directory = searchPath,
                min_confidence_threshold = minConfidence,
                total_patterns_found = patterns.Count,
                patterns_above_threshold = filteredPatterns.Count,
                patterns = filteredPatterns.Select(pattern => new
                {
                    type = pattern.Type.ToString(),
                    name = pattern.Name,
                    description = pattern.Description,
                    confidence_score = Math.Round(pattern.ConfidenceScore, 3),
                    root_path = pattern.RootPath,
                    project_count = pattern.ProjectPaths?.Count ?? 0,
                    project_paths = pattern.ProjectPaths,
                    technologies = pattern.Technologies,
                    detection_reasons = pattern.DetectionReasons,
                    indicators = pattern.Indicators?.Select(indicator => new
                    {
                        type = indicator.Type,
                        value = indicator.Value,
                        location = indicator.Location,
                        weight = indicator.Weight,
                        description = indicator.Description
                    }).ToArray()
                }).ToArray(),
                analysis_summary = new
                {
                    most_confident_pattern = filteredPatterns.OrderByDescending(p => p.ConfidenceScore).FirstOrDefault()?.Name,
                    unique_technologies = patterns.SelectMany(p => p.Technologies ?? []).Distinct().ToArray(),
                    // RS-004: Using extracted service instead of helper method
                    complexity_assessment = ArchitectureRecommendationService.GetComplexityAssessment(filteredPatterns),
                    architecture_recommendations = ArchitectureRecommendationService.GenerateDetailedArchitectureRecommendations(filteredPatterns)
                }
            });
        });
    }

    [McpServerTool]
    [Description("Suggest related workspaces based on detected architecture patterns and project relationships")]
    public async Task<string> SuggestRelatedWorkspacesAsync(
        [Description("Base directory to search from (uses current workspace if empty)")]
        string? baseDirectory = null,
        [Description("Maximum number of workspace suggestions to return")]
        int maxSuggestions = 10)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            string searchBase = string.IsNullOrEmpty(baseDirectory) ? config.DefaultWorkspace : baseDirectory;

            // Start from parent directory to find related workspaces
            string parentDir = Directory.GetParent(searchBase)?.FullName ?? searchBase;

            if (!Directory.Exists(parentDir))
            {
                throw new DirectoryNotFoundException($"Parent directory does not exist: {parentDir}");
            }

            var suggestions = new List<object>();
            List<ArchitecturePattern> currentPatterns = await architectureDetection.DetectArchitecturePatternsAsync(searchBase);

            // Look for related projects in sibling directories
            IEnumerable<string> siblingDirectories = Directory.GetDirectories(parentDir, "*", SearchOption.TopDirectoryOnly)
                .Where(dir => !IsExcludedDirectory(dir) && !dir.Equals(searchBase, StringComparison.OrdinalIgnoreCase))
                .Take(15); // Limit for performance

            List<string> directories = siblingDirectories.ToList();
            foreach (string siblingDir in directories)
            {
                try
                {
                    ProjectInfo projectInfo = await projectDetection.AnalyzeDirectoryAsync(siblingDir);
                    if (projectInfo.Type != ProjectType.Unknown)
                    {
                        List<ArchitecturePattern> siblingPatterns = await architectureDetection.DetectArchitecturePatternsAsync(siblingDir);
                        
                        // RS-004: Using extracted service instead of helper method
                        double relationshipScore = relationshipScoring.CalculateRelationshipScore(currentPatterns, siblingPatterns, projectInfo);

                        if (relationshipScore > 0.3) // Minimum relationship threshold
                        {
                            suggestions.Add(new
                            {
                                path = siblingDir,
                                name = projectInfo.Name,
                                type = projectInfo.Type.ToString(),
                                description = projectInfo.Description,
                                relationship_score = Math.Round(relationshipScore, 3),
                                // RS-004: Using extracted service instead of helper method
                                relationship_reasons = RelationshipScoringService.GenerateRelationshipReasons(currentPatterns, siblingPatterns, projectInfo),
                                indicators = projectInfo.Indicators,
                                detected_patterns = siblingPatterns.Select(p => p.Name).ToArray()
                            });
                        }
                    }
                }
                catch
                {
                    // Skip directories that can't be analyzed
                    continue;
                }
            }

            // Sort by relationship score
            object[] sortedSuggestions = suggestions
                .OrderByDescending(s => (double)((dynamic)s).relationship_score)
                .Take(maxSuggestions)
                .ToArray();

            return CreateSuccessResponse(new
            {
                current_workspace = searchBase,
                search_base = parentDir,
                total_suggestions = sortedSuggestions.Length,
                current_workspace_patterns = currentPatterns.Select(p => new
                {
                    name = p.Name,
                    type = p.Type.ToString(),
                    confidence = Math.Round(p.ConfidenceScore, 3)
                }).ToArray(),
                suggested_workspaces = sortedSuggestions,
                search_summary = new
                {
                    directories_analyzed = directories.Count(),
                    patterns_in_current = currentPatterns.Count,
                    high_confidence_suggestions = sortedSuggestions.Count(s => (double)((dynamic)s).relationship_score > 0.7),
                    moderate_confidence_suggestions = sortedSuggestions.Count(s => (double)((dynamic)s).relationship_score > 0.5)
                }
            });
        });
    }

    #endregion

    #region Helper Methods

    // RS-004: Kept minimal essential helper methods, complex logic moved to services
    private static bool IsExcludedDirectory(string directory)
    {
        string dirName = Path.GetFileName(directory).ToLowerInvariant();
        var excludedDirs = new[] { ".git", ".vs", ".vscode", "bin", "obj", "node_modules", "packages", "target", "dist", "build", ".angular" };
        return excludedDirs.Contains(dirName);
    }

    #endregion
}
