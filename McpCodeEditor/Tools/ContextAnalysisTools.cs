using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools;

/// <summary>
/// Tools for analyzing current project context and providing intelligent file suggestions.
/// </summary>
[McpServerToolType]
public class ContextAnalysisTools(
    ContextAnalysisService analysis,
    ProjectScaleService projectScale,
    CodeEditorConfigurationService config,
    SuggestionRationaleService rationaleService)
    : BaseToolClass
{
    [McpServerTool]
    [Description("Analyze current project context and get relevant files prioritized by importance")]
    public async Task<string> ContextAnalyzeCurrentAsync(
        [Description("Optional directory to focus analysis on (defaults to current workspace)")]
        string? focusDirectory = null,
        [Description("Maximum number of files to return (default: 20)")]
        int maxFiles = 20)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var result = await analysis.AnalyzeCurrentContextAsync(focusDirectory, maxFiles);
            
            if (!result.Success)
            {
                return new 
                { 
                    success = false, 
                    error = result.Error 
                };
            }

            return CreateSuccessResponse(new
            {
                workspace_path = result.WorkspacePath,
                project_info = result.ProjectInfo != null ? new
                {
                    name = result.ProjectInfo.Name,
                    type = result.ProjectInfo.Type.ToString(),
                    description = result.ProjectInfo.Description,
                    indicators = result.ProjectInfo.Indicators,
                    score = result.ProjectInfo.Score
                } : null,
                relevant_files = result.RelevantFiles?.Select(f => new
                {
                    file_path = f.FilePath,
                    relative_path = f.RelativePath,
                    file_type = f.FileType,
                    relevance_score = f.RelevanceScore,
                    last_modified = f.LastModified.ToString("yyyy-MM-dd HH:mm:ss"),
                    size_bytes = f.SizeBytes,
                    size_kb = Math.Round(f.SizeBytes / 1024.0, 1),
                    reason = f.ReasonForRelevance
                }).ToArray() ?? Array.Empty<object>(),
                analysis_summary = new
                {
                    total_files_analyzed = result.TotalFilesAnalyzed,
                    source_code_files = result.RelevantFiles?.Count(f => f.RelevanceScore >= 80) ?? 0,
                    config_files = result.RelevantFiles?.Count(f => f.RelevanceScore is >= 60 and < 80) ?? 0,
                    documentation_files = result.RelevantFiles?.Count(f => f.RelevanceScore is >= 40 and < 60) ?? 0,
                    recently_modified = result.RelevantFiles?.Count(f => f.LastModified > DateTime.Now.AddDays(-30)) ?? 0
                },
                filtering_applied = result.FilteringApplied != null ? new
                {
                    excluded_directories = result.FilteringApplied.ExcludedDirectories,
                    excluded_patterns = result.FilteringApplied.ExcludedPatterns,
                    file_type_priorities = result.FilteringApplied.FileTypePriorities
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(10)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                } : null
            });
        });
    }

    [McpServerTool]
    [Description("Get personalized file suggestions based on project type and recent activity")]
    public async Task<string> ContextGetPersonalizedSuggestionsAsync(
        [Description("Optional directory to focus suggestions on (defaults to current workspace)")]
        string? focusDirectory = null,
        [Description("Maximum number of suggestions to return (default: 10)")]
        int maxSuggestions = 10,
        [Description("Minimum relevance score for suggestions (default: 50)")]
        int minRelevanceScore = 50)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var suggestions = await analysis.GetPersonalizedSuggestionsAsync(focusDirectory, maxSuggestions);
            
            // Get detailed analysis for context
            var fullAnalysis = await analysis.AnalyzeCurrentContextAsync(focusDirectory, 50);
            
            return CreateSuccessResponse(new
            {
                workspace_path = focusDirectory ?? config.DefaultWorkspace,
                suggested_files = suggestions,
                suggestions_count = suggestions.Count,
                project_type = fullAnalysis.ProjectInfo?.Type.ToString() ?? "Unknown",
                rationale = SuggestionRationaleService.GenerateSuggestionRationale(fullAnalysis, suggestions),
                alternative_suggestions = fullAnalysis.RelevantFiles?
                    .Where(f => f.RelevanceScore >= minRelevanceScore && !suggestions.Contains(f.FilePath))
                    .OrderByDescending(f => f.RelevanceScore)
                    .Take(5)
                    .Select(f => new
                    {
                        file_path = f.FilePath,
                        relative_path = f.RelativePath,
                        relevance_score = f.RelevanceScore,
                        reason = f.ReasonForRelevance
                    })
                    .ToArray() ?? Array.Empty<object>(),
                tips = new[]
                {
                    "Files are ranked by relevance based on type, recency, and project importance",
                    "Source code files (.cs, .js, .py) are prioritized highest",
                    "Recently modified files get bonus relevance points",
                    "Build artifacts and backup files are automatically excluded"
                }
            });
        });
    }

    [McpServerTool]
    [Description("Get context analysis statistics with intelligent build artifact filtering (UX-008 enhanced)")]
    public async Task<string> ContextGetFilteringStatsAsync(
        [Description("Optional directory to analyze (defaults to current workspace)")]
        string? focusDirectory = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var workspacePath = focusDirectory ?? config.DefaultWorkspace;
            
            if (!Directory.Exists(workspacePath))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {workspacePath}");
            }

            // Use intelligent project scale analysis instead of naive file counting
            var scaleAnalysis = await ProjectScaleService.AnalyzeProjectScaleAsync(workspacePath);
            var artifactDetection = await ProjectScaleService.DetectBuildArtifactsAsync(workspacePath);
            
            // Get context analysis with intelligent filtering
            var contextAnalysis = await analysis.AnalyzeCurrentContextAsync(focusDirectory, 1000);
            
            return CreateSuccessResponse(new
            {
                workspace_path = workspacePath,
                intelligent_analysis = new
                {
                    note = "UX-008 Enhanced: Uses intelligent filtering to exclude build artifacts and provide accurate metrics",
                    total_files_found = scaleAnalysis.TotalFiles,
                    source_files = scaleAnalysis.SourceFiles.Count,
                    build_artifacts = scaleAnalysis.BuildArtifacts.Count,
                    generated_files = scaleAnalysis.GeneratedFiles.Count,
                    other_files = scaleAnalysis.OtherFiles.Count,
                    accuracy_improvement = artifactDetection.RecommendExclusion ? 
                        $"CRITICAL: Filtered {artifactDetection.ArtifactPercentage:F1}% build artifacts - raw count would be misleading!" :
                        $"Good: Only {artifactDetection.ArtifactPercentage:F1}% are build artifacts",
                    true_project_complexity = $"{scaleAnalysis.SourceFiles.Count} source files (not {scaleAnalysis.TotalFiles} total)"
                },
                file_statistics = new
                {
                    source_files = scaleAnalysis.SourceFiles.Count,
                    relevant_for_context = contextAnalysis.RelevantFiles?.Count ?? 0,
                    context_relevance_rate = scaleAnalysis.SourceFiles.Count > 0 ? 
                        Math.Round((double)(contextAnalysis.RelevantFiles?.Count ?? 0) / scaleAnalysis.SourceFiles.Count * 100, 1) : 0,
                    exclusion_effectiveness = new
                    {
                        build_artifacts_excluded = scaleAnalysis.BuildArtifacts.Count,
                        generated_files_excluded = scaleAnalysis.GeneratedFiles.Count,
                        low_relevance_files_filtered = contextAnalysis.RelevantFiles?.Count(f => f.RelevanceScore < 30) ?? 0,
                        total_filtering_impact = $"Excluded {scaleAnalysis.BuildArtifacts.Count + scaleAnalysis.GeneratedFiles.Count} non-source files"
                    }
                },
                artifact_breakdown = artifactDetection.LargestArtifactCategories.Select(cat => new
                {
                    category = cat.Name,
                    file_count = cat.FileCount,
                    percentage_of_total = scaleAnalysis.TotalFiles > 0 ? 
                        Math.Round((double)cat.FileCount / scaleAnalysis.TotalFiles * 100, 1) : 0
                }).ToArray(),
                file_type_distribution = contextAnalysis.RelevantFiles?
                    .GroupBy(f => f.FileType)
                    .Select(g => new
                    {
                        file_type = g.Key,
                        count = g.Count(),
                        avg_relevance = Math.Round(g.Average(f => f.RelevanceScore), 1),
                        most_relevant = g.OrderByDescending(f => f.RelevanceScore).First().FilePath
                    })
                    .OrderByDescending(g => g.count)
                    .Take(10)
                    .ToArray() ?? Array.Empty<object>(),
                filtering_effectiveness = new
                {
                    source_code_percentage = contextAnalysis.RelevantFiles?.Count > 0 ? 
                        Math.Round((double)(contextAnalysis.RelevantFiles?.Count(f => f.RelevanceScore >= 80) ?? 0) / contextAnalysis.RelevantFiles?.Count ?? 0 * 100, 1) : 0,
                    build_artifacts_excluded = true,
                    generated_files_excluded = true,
                    backup_files_excluded = true,
                    temp_files_excluded = true,
                    node_modules_excluded = scaleAnalysis.BuildArtifacts.Any(f => f.RelativePath.Contains("node_modules")),
                    bin_obj_excluded = scaleAnalysis.BuildArtifacts.Any(f => f.RelativePath.Contains("bin") || f.RelativePath.Contains("obj"))
                },
                recommendations = new
                {
                    use_source_metrics = artifactDetection.RecommendExclusion,
                    reason = artifactDetection.RecommendExclusion ? 
                        "Use source-only file counts for accurate project assessment" :
                        "Total file counts are reasonably accurate for this project",
                    context_optimization = contextAnalysis.RelevantFiles?.Count < 50 ? 
                        "Project size is optimal for context analysis" :
                        "Consider focusing on specific directories for targeted analysis"
                }
            });
        });
    }
}
