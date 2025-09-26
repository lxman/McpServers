using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools;

/// <summary>
/// Focused class for project scale analysis operations following Single Responsibility Principle
/// </summary>
public class ProjectAnalysisTools(
    ProjectScaleService projectScale,
    CodeEditorConfigurationService config)
    : BaseToolClass
{
    [McpServerTool]
    [Description("Analyze project scale with intelligent filtering of build artifacts and dependencies")]
    public async Task<string> AnalyzeProjectScaleAsync(
        [Description("Project path to analyze (defaults to current workspace)")]
        string? projectPath = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            string analysisPath = projectPath ?? config.DefaultWorkspace;
            ProjectScaleAnalysis analysis = await ProjectScaleService.AnalyzeProjectScaleAsync(analysisPath);
            
            var result = new
            {
                success = true,
                project_path = analysis.ProjectPath,
                analyzed_at = analysis.AnalyzedAt,
                summary = new
                {
                    total_files = analysis.TotalFiles,
                    source_files = analysis.SourceFiles.Count,
                    generated_files = analysis.GeneratedFiles.Count,
                    build_artifacts = analysis.BuildArtifacts.Count,
                    other_files = analysis.OtherFiles.Count,
                    total_size_mb = Math.Round(analysis.TotalSizeBytes / 1024.0 / 1024.0, 2),
                    source_percentage = analysis.TotalFiles > 0 ? 
                        Math.Round((double)analysis.SourceFiles.Count / analysis.TotalFiles * 100, 1) : 0,
                    artifact_percentage = analysis.TotalFiles > 0 ? 
                        Math.Round((double)analysis.BuildArtifacts.Count / analysis.TotalFiles * 100, 1) : 0
                },
                categorization = new
                {
                    source_files = analysis.SourceFiles.Take(10).Select(f => f.RelativePath).ToArray(),
                    largest_artifacts = analysis.BuildArtifacts
                        .OrderByDescending(f => f.SizeBytes)
                        .Take(5)
                        .Select(f => new { f.RelativePath, size_mb = Math.Round(f.SizeBytes / 1024.0 / 1024.0, 2) })
                        .ToArray(),
                    generated_files_sample = analysis.GeneratedFiles.Take(5).Select(f => f.RelativePath).ToArray()
                }
            };

            return result;
        });
    }

    [McpServerTool]
    [Description("Get source file statistics excluding build artifacts - provides accurate project complexity metrics")]
    public async Task<string> GetSourceStatisticsAsync(
        [Description("Project path to analyze (defaults to current workspace)")]
        string? projectPath = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            string analysisPath = projectPath ?? config.DefaultWorkspace;
            SourceFileStatistics stats = await ProjectScaleService.GetSourceStatisticsAsync(analysisPath);
            
            var result = new
            {
                success = true,
                project_path = stats.ProjectPath,
                intelligent_analysis = new
                {
                    source_files = stats.SourceFileCount,
                    total_files = stats.TotalFileCount,
                    exclusion_summary = $"Excluded {stats.BuildArtifactCount + stats.GeneratedFileCount} build artifacts and generated files",
                    accuracy_improvement = stats.TotalFileCount > 0 ? 
                        $"Reduced from {stats.TotalFileCount} to {stats.SourceFileCount} files ({Math.Round((double)stats.SourceFileCount / stats.TotalFileCount * 100, 1)}% are actual source)"
                        : "No files found"
                },
                project_scale = new
                {
                    classification = stats.ProjectScale.ToString(),
                    complexity_score = stats.SourceComplexityScore,
                    scale_description = GetScaleDescription(stats.ProjectScale),
                    size_mb = Math.Round(stats.SourceSizeBytes / 1024.0 / 1024.0, 2),
                    total_size_mb = Math.Round(stats.TotalSizeBytes / 1024.0 / 1024.0, 2)
                },
                language_breakdown = stats.LanguageBreakdown,
                filtering_applied = new
                {
                    excluded_patterns_count = stats.ExcludedPatterns.Count,
                    build_artifacts_excluded = stats.BuildArtifactCount,
                    generated_files_excluded = stats.GeneratedFileCount,
                    key_exclusions = stats.ExcludedPatterns.Take(10).ToArray()
                }
            };

            return result;
        });
    }

    [McpServerTool]
    [Description("Detect build artifacts and assess their impact on project file counts")]
    public async Task<string> DetectBuildArtifactsAsync(
        [Description("Project path to analyze (defaults to current workspace)")]
        string? projectPath = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            string analysisPath = projectPath ?? config.DefaultWorkspace;
            BuildArtifactDetectionResult detection = await ProjectScaleService.DetectBuildArtifactsAsync(analysisPath);
            
            var result = new
            {
                success = true,
                project_path = detection.ProjectPath,
                artifact_analysis = new
                {
                    total_files = detection.TotalFiles,
                    source_files = detection.SourceFiles,
                    build_artifacts = detection.BuildArtifacts,
                    generated_files = detection.GeneratedFiles,
                    artifact_percentage = Math.Round(detection.ArtifactPercentage, 1),
                    problem_detected = detection.RecommendExclusion,
                    impact_assessment = detection.RecommendExclusion ? 
                        $"CRITICAL: {detection.ArtifactPercentage:F1}% of files are build artifacts - this severely skews complexity metrics!"
                        : $"Acceptable: Only {detection.ArtifactPercentage:F1}% are artifacts"
                },
                recommendations = new
                {
                    exclude_artifacts = detection.RecommendExclusion,
                    reason = detection.RecommendExclusion ? 
                        "Build artifacts outnumber source files - use source-only metrics for accurate analysis"
                        : "Artifact ratio is acceptable - total file counts are reasonably accurate",
                    corrected_metrics = $"True project size: {detection.SourceFiles} source files (not {detection.TotalFiles} total)"
                },
                largest_categories = detection.LargestArtifactCategories.Select(cat => new
                {
                    category = cat.Name,
                    file_count = cat.FileCount,
                    percentage = detection.TotalFiles > 0 ? 
                        Math.Round((double)cat.FileCount / detection.TotalFiles * 100, 1) : 0
                }).ToArray()
            };

            return result;
        });
    }

    #region Helper Methods

    private static string GetScaleDescription(ProjectScale scale)
    {
        return scale switch
        {
            ProjectScale.Tiny => "Tiny project - ideal for learning or prototypes",
            ProjectScale.Small => "Small project - suitable for simple applications",
            ProjectScale.Medium => "Medium project - typical business application",
            ProjectScale.Large => "Large project - complex application with multiple modules",
            ProjectScale.VeryLarge => "Very large project - enterprise application",
            ProjectScale.Enterprise => "Enterprise scale - major system with extensive codebase",
            _ => "Unknown scale"
        };
    }

    #endregion
}
