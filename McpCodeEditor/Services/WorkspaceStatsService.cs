namespace McpCodeEditor.Services;

/// <summary>
/// RS-001: Service for workspace statistics operations
/// Extracted from WorkspaceTools.cs to follow Single Responsibility Principle
/// Handles both intelligent and legacy workspace statistics
/// </summary>
public class WorkspaceStatsService(
    CodeEditorConfigurationService config,
    ProjectScaleService projectScale)
{
    /// <summary>
    /// Gets intelligent workspace statistics using project scale analysis
    /// Excludes build artifacts and provides accurate metrics
    /// </summary>
    /// <returns>Intelligent statistics object or null if analysis fails</returns>
    public async Task<object?> GetIntelligentWorkspaceStatsAsync()
    {
        try
        {
            var workspace = config.DefaultWorkspace;
            if (!Directory.Exists(workspace))
            {
                return null;
            }

            // Use the new intelligent project scale analysis
            var stats = await ProjectScaleService.GetSourceStatisticsAsync(workspace);
            
            return new
            {
                // NEW: Intelligent metrics that exclude build artifacts
                source_files = stats.SourceFileCount,
                total_files = stats.TotalFileCount,
                build_artifacts_excluded = stats.BuildArtifactCount,
                generated_files_excluded = stats.GeneratedFileCount,
                
                // Project scale classification
                project_scale = stats.ProjectScale.ToString(),
                complexity_score = stats.SourceComplexityScore,
                scale_description = GetScaleDescription(stats.ProjectScale),
                
                // Size information
                source_size_mb = Math.Round(stats.SourceSizeBytes / 1024.0 / 1024.0, 2),
                total_size_mb = Math.Round(stats.TotalSizeBytes / 1024.0 / 1024.0, 2),
                
                // Language breakdown
                language_breakdown = stats.LanguageBreakdown,
                
                // Filtering summary
                filtering_applied = new
                {
                    accuracy_improvement = $"Filtered from {stats.TotalFileCount} to {stats.SourceFileCount} relevant files",
                    artifact_percentage = stats.TotalFileCount > 0 ? 
                        Math.Round((double)(stats.BuildArtifactCount + stats.GeneratedFileCount) / stats.TotalFileCount * 100, 1) : 0,
                    exclusion_note = "Build artifacts, dependencies, and generated files excluded from complexity metrics"
                }
            };
        }
        catch
        {
            // Fallback to old method if new analysis fails
            return await GetLegacyWorkspaceStatsAsync();
        }
    }

    /// <summary>
    /// Gets legacy workspace statistics (fallback method)
    /// Used when intelligent analysis fails
    /// </summary>
    /// <returns>Legacy statistics object or null if analysis fails</returns>
    public async Task<object?> GetLegacyWorkspaceStatsAsync()
    {
        try
        {
            var workspace = config.DefaultWorkspace;
            if (!Directory.Exists(workspace))
            {
                return null;
            }

            var files = Directory.GetFiles(workspace, "*", SearchOption.AllDirectories)
                .Where(file => config.AllowedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .ToArray();

            var totalSize = files.Sum(file => new FileInfo(file).Length);

            var extensionStats = files
                .GroupBy(file => Path.GetExtension(file).ToLowerInvariant())
                .Select(group => new
                {
                    extension = group.Key,
                    count = group.Count(),
                    total_size = group.Sum(file => new FileInfo(file).Length)
                })
                .OrderByDescending(stat => stat.count)
                .ToArray();

            return new
            {
                total_files = files.Length,
                total_size_bytes = totalSize,
                total_size_mb = Math.Round(totalSize / 1024.0 / 1024.0, 2),
                file_types = extensionStats,
                note = "This is legacy analysis - use GetSourceStatistics for accurate metrics excluding build artifacts"
            };
        }
        catch
        {
            return null;
        }
    }

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
}
