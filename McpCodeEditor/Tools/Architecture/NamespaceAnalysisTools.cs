using McpCodeEditor.Models.Analysis;
using McpCodeEditor.Services;
using McpCodeEditor.Services.Analysis;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace McpCodeEditor.Tools.Architecture;

/// <summary>
/// MCP tools for namespace dependency analysis and coupling patterns.
/// Focused responsibility: Namespace analysis, platform boundaries, and coupling strength analysis.
/// </summary>
[McpServerToolType]
public class NamespaceAnalysisTools(
    CodeEditorConfigurationService config,
    NamespaceDependencyAnalyzer namespaceDependencyAnalyzer)
{
    #region Namespace Dependency Analysis MCP Tools

    [McpServerTool]
    [Description("Analyze namespace dependencies and coupling patterns across the workspace")]
    public async Task<string> AnalyzeNamespaceDependenciesAsync(
        [Description("Workspace path to analyze (uses current workspace if empty)")]
        string? workspacePath = null)
    {
        try
        {
            string searchPath = string.IsNullOrEmpty(workspacePath) ? config.DefaultWorkspace : workspacePath;

            if (!Directory.Exists(searchPath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Directory does not exist: {searchPath}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            NamespaceDependencyAnalysis analysis = await namespaceDependencyAnalyzer.AnalyzeNamespaceDependenciesAsync(searchPath);

            var result = new
            {
                success = true,
                workspace_path = searchPath,
                analysis_date = analysis.AnalysisDate,
                summary = new
                {
                    total_namespaces = analysis.TotalNamespaces,
                    total_couplings = analysis.TotalCouplings,
                    average_coupling_strength = Math.Round(analysis.AverageCouplingStrength, 3),
                    platform_boundaries_detected = analysis.PlatformBoundaries.Count,
                    architectural_patterns_detected = analysis.DetectedPatterns.Count
                },
                namespace_couplings = analysis.Couplings
                    .OrderByDescending(c => c.CouplingStrength)
                    .Take(20) // Show top 20 strongest couplings
                    .Select(c => new
                    {
                        source_namespace = c.SourceNamespace,
                        target_namespace = c.TargetNamespace,
                        usage_count = c.UsageCount,
                        coupling_strength = Math.Round(c.CouplingStrength, 3),
                        is_internal_coupling = c.IsInternalCoupling,
                        is_cross_platform = c.IsCrossPlatformCoupling,
                        source_platform = c.SourcePlatform,
                        target_platform = c.TargetPlatform,
                        source_files_count = c.SourceFiles.Count,
                        source_files = c.SourceFiles.Take(5).ToArray() // Show sample files
                    }).ToArray(),
                platform_boundaries = analysis.PlatformBoundaries.Select(p => new
                {
                    platform_name = p.PlatformName,
                    platform_type = p.Type.ToString(),
                    namespace_pattern = p.NamespacePattern,
                    namespaces_count = p.Namespaces.Count,
                    namespaces = p.Namespaces.Take(10).ToArray(), // Show sample namespaces
                    is_isolated = p.IsIsolated,
                    isolation_score = Math.Round(p.IsolationScore, 3),
                    internal_coupling_count = p.InternalCouplingCount,
                    external_coupling_count = p.ExternalCouplingCount,
                    external_couplings = p.ExternalCouplings.Take(5).ToArray(), // Show sample external couplings
                    shared_dependencies = p.SharedDependencies.Take(5).ToArray(),
                    projects_count = p.Projects.Count
                }).OrderByDescending(p => p.isolation_score).ToArray(),
                detected_patterns = analysis.DetectedPatterns,
                recommendations = GenerateNamespaceRecommendations(analysis)
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Detect and analyze platform boundaries and isolation characteristics")]
    public async Task<string> DetectPlatformBoundariesAsync(
        [Description("Workspace path to analyze (uses current workspace if empty)")]
        string? workspacePath = null,
        [Description("Minimum isolation score threshold (0.0-1.0)")]
        double minIsolationScore = 0.0)
    {
        try
        {
            string searchPath = string.IsNullOrEmpty(workspacePath) ? config.DefaultWorkspace : workspacePath;

            if (!Directory.Exists(searchPath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Directory does not exist: {searchPath}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            NamespaceDependencyAnalysis analysis = await namespaceDependencyAnalyzer.AnalyzeNamespaceDependenciesAsync(searchPath);
            List<PlatformBoundary> filteredBoundaries = analysis.PlatformBoundaries
                .Where(p => p.IsolationScore >= minIsolationScore)
                .OrderByDescending(p => p.IsolationScore)
                .ToList();

            var result = new
            {
                success = true,
                workspace_path = searchPath,
                analysis_date = analysis.AnalysisDate,
                min_isolation_threshold = minIsolationScore,
                summary = new
                {
                    total_platforms_detected = analysis.PlatformBoundaries.Count,
                    platforms_above_threshold = filteredBoundaries.Count,
                    highly_isolated_platforms = filteredBoundaries.Count(p => p.IsolationScore > 0.8),
                    moderately_isolated_platforms = filteredBoundaries.Count(p => p.IsolationScore is > 0.5 and <= 0.8),
                    coupled_platforms = analysis.PlatformBoundaries.Count(p => p.IsolationScore <= 0.5)
                },
                platform_boundaries = filteredBoundaries.Select(p => new
                {
                    platform_name = p.PlatformName,
                    platform_type = p.Type.ToString(),
                    namespace_pattern = p.NamespacePattern,
                    isolation_assessment = GetIsolationAssessment(p.IsolationScore),
                    isolation_score = Math.Round(p.IsolationScore, 3),
                    is_perfectly_isolated = p.IsIsolated,
                    namespace_details = new
                    {
                        total_namespaces = p.Namespaces.Count,
                        sample_namespaces = p.Namespaces.Take(5).ToArray(),
                        internal_coupling_count = p.InternalCouplingCount,
                        external_coupling_count = p.ExternalCouplingCount
                    },
                    boundary_violations = new
                    {
                        has_violations = p.ExternalCouplingCount > 0,
                        violation_count = p.ExternalCouplingCount,
                        external_dependencies = p.ExternalCouplings.Take(10).ToArray()
                    },
                    shared_components = new
                    {
                        shared_dependency_count = p.SharedDependencies.Count,
                        shared_dependencies = p.SharedDependencies.Take(5).ToArray()
                    },
                    projects = p.Projects.Take(5).ToArray()
                }).ToArray(),
                isolation_recommendations = GenerateIsolationRecommendations(filteredBoundaries),
                architecture_assessment = AssessArchitecturalStrategy(analysis)
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Analyze coupling strength and identify highly coupled components")]
    public async Task<string> AnalyzeCouplingStrengthAsync(
        [Description("Workspace path to analyze (uses current workspace if empty)")]
        string? workspacePath = null,
        [Description("Minimum coupling strength threshold (0.0-1.0)")]
        double minCouplingStrength = 0.3,
        [Description("Focus on internal project couplings only")]
        bool internalOnly = true)
    {
        try
        {
            string searchPath = string.IsNullOrEmpty(workspacePath) ? config.DefaultWorkspace : workspacePath;

            if (!Directory.Exists(searchPath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Directory does not exist: {searchPath}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            NamespaceDependencyAnalysis analysis = await namespaceDependencyAnalyzer.AnalyzeNamespaceDependenciesAsync(searchPath);
            List<NamespaceCoupling> filteredCouplings = analysis.Couplings
                .Where(c => c.CouplingStrength >= minCouplingStrength && (!internalOnly || c.IsInternalCoupling))
                .OrderByDescending(c => c.CouplingStrength)
                .ToList();

            var result = new
            {
                success = true,
                workspace_path = searchPath,
                analysis_date = analysis.AnalysisDate,
                filter_settings = new
                {
                    min_coupling_strength = minCouplingStrength,
                    internal_only = internalOnly
                },
                summary = new
                {
                    total_couplings_found = analysis.Couplings.Count,
                    couplings_above_threshold = filteredCouplings.Count,
                    high_coupling_relationships = filteredCouplings.Count(c => c.CouplingStrength > 0.7),
                    moderate_coupling_relationships = filteredCouplings.Count(c => c.CouplingStrength is > 0.4 and <= 0.7),
                    cross_platform_violations = filteredCouplings.Count(c => c.IsCrossPlatformCoupling),
                    average_coupling_strength = Math.Round(filteredCouplings.Count != 0 ? filteredCouplings.Average(c => c.CouplingStrength) : 0.0, 3)
                },
                coupling_relationships = filteredCouplings.Take(25).Select(c => new
                {
                    source_namespace = c.SourceNamespace,
                    target_namespace = c.TargetNamespace,
                    coupling_strength = Math.Round(c.CouplingStrength, 3),
                    coupling_assessment = GetCouplingAssessment(c.CouplingStrength),
                    usage_details = new
                    {
                        usage_count = c.UsageCount,
                        file_spread = c.SourceFiles.Count,
                        sample_files = c.SourceFiles.Take(3).Select(f => Path.GetFileName(f)).ToArray()
                    },
                    relationship_type = new
                    {
                        is_internal_coupling = c.IsInternalCoupling,
                        is_cross_platform = c.IsCrossPlatformCoupling,
                        source_platform = c.SourcePlatform,
                        target_platform = c.TargetPlatform
                    }
                }).ToArray(),
                coupling_hotspots = IdentifyCouplingHotspots(filteredCouplings),
                refactoring_suggestions = GenerateCouplingRefactoringSuggestions(filteredCouplings)
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Namespace Analysis Helper Methods

    private static string[] GenerateNamespaceRecommendations(NamespaceDependencyAnalysis analysis)
    {
        var recommendations = new List<string>();

        // Check for high coupling patterns
        List<NamespaceCoupling> highCouplings = analysis.Couplings.Where(c => c.CouplingStrength > 0.7).ToList();
        if (highCouplings.Count != 0)
        {
            recommendations.Add($"?? Consider refactoring {highCouplings.Count} high-coupling relationships (strength > 0.7)");
        }

        // Check for cross-platform violations
        List<NamespaceCoupling> crossPlatformViolations = analysis.Couplings.Where(c => c.IsCrossPlatformCoupling).ToList();
        if (crossPlatformViolations.Count != 0)
        {
            recommendations.Add($"?? {crossPlatformViolations.Count} cross-platform boundary violations detected - consider introducing abstractions");
        }

        // Check for isolated platforms
        List<PlatformBoundary> isolatedPlatforms = analysis.PlatformBoundaries.Where(p => p.IsolationScore > 0.8).ToList();
        if (isolatedPlatforms.Count >= 2)
        {
            recommendations.Add($"? Excellent platform isolation detected in {isolatedPlatforms.Count} platforms - maintain this separation");
        }

        // Check for shared dependencies
        List<PlatformBoundary> platformsWithShared = analysis.PlatformBoundaries.Where(p => p.SharedDependencies.Count != 0).ToList();
        if (platformsWithShared.Count != 0)
        {
            recommendations.Add($"?? {platformsWithShared.Count} platforms use shared dependencies - ensure version compatibility");
        }

        return recommendations.ToArray();
    }

    private static string GetIsolationAssessment(double isolationScore)
    {
        return isolationScore switch
        {
            >= 0.9 => "Excellent - Highly isolated",
            >= 0.7 => "Good - Well isolated",
            >= 0.5 => "Moderate - Some coupling",
            >= 0.3 => "Poor - Significant coupling",
            _ => "Critical - Highly coupled"
        };
    }

    private static string[] GenerateIsolationRecommendations(List<PlatformBoundary> boundaries)
    {
        var recommendations = new List<string>();

        List<PlatformBoundary> coupledPlatforms = boundaries.Where(p => p.IsolationScore < 0.5).ToList();
        if (coupledPlatforms.Count != 0)
        {
            recommendations.Add($"?? Refactor {coupledPlatforms.Count} highly coupled platforms to improve isolation");
        }

        List<PlatformBoundary> violatingPlatforms = boundaries.Where(p => p.ExternalCouplingCount > 0).ToList();
        if (violatingPlatforms.Count != 0)
        {
            recommendations.Add($"?? Introduce abstraction layers for {violatingPlatforms.Count} platforms with boundary violations");
        }

        List<PlatformBoundary> isolatedPlatforms = boundaries.Where(p => p.IsIsolated).ToList();
        if (isolatedPlatforms.Count >= 2)
        {
            recommendations.Add($"? Perfect isolation achieved in {isolatedPlatforms.Count} platforms - excellent architecture!");
        }

        return recommendations.ToArray();
    }

    private static object AssessArchitecturalStrategy(NamespaceDependencyAnalysis analysis)
    {
        int isolatedCount = analysis.PlatformBoundaries.Count(p => p.IsolationScore > 0.8);
        int totalPlatforms = analysis.PlatformBoundaries.Count;
        int crossPlatformViolations = analysis.Couplings.Count(c => c.IsCrossPlatformCoupling);

        string strategy;
        string assessment;

        if (isolatedCount >= 2 && crossPlatformViolations == 0)
        {
            strategy = "Parallel Platform Strategy";
            assessment = "Excellent - Clean platform separation with no boundary violations";
        }
        else if (isolatedCount >= 1 && crossPlatformViolations < 5)
        {
            strategy = "Hybrid Architecture";
            assessment = "Good - Mixed isolation with minimal coupling";
        }
        else if (analysis.PlatformBoundaries.Any(p => p.Type == PlatformType.Core) && 
                 analysis.Couplings.Any(c => c.TargetPlatform == "Core"))
        {
            strategy = "Shared Core Architecture";
            assessment = "Moderate - Central shared components with consumer platforms";
        }
        else
        {
            strategy = "Monolithic Architecture";
            assessment = "Needs improvement - High coupling between components";
        }

        return new
        {
            detected_strategy = strategy,
            assessment = assessment,
            isolation_ratio = totalPlatforms > 0 ? Math.Round((double)isolatedCount / totalPlatforms, 2) : 0.0,
            boundary_violation_count = crossPlatformViolations,
            total_platforms = totalPlatforms,
            recommendation = GetArchitecturalRecommendation(strategy, assessment)
        };
    }

    private static string GetArchitecturalRecommendation(string strategy, string assessment)
    {
        return strategy switch
        {
            "Parallel Platform Strategy" => "Maintain excellent isolation - consider extracting common utilities to shared libraries",
            "Hybrid Architecture" => "Work towards better platform isolation by reducing cross-platform dependencies",
            "Shared Core Architecture" => "Ensure core components have stable APIs and clear versioning",
            "Monolithic Architecture" => "Consider extracting modules into separate platforms with clear boundaries",
            _ => "Continue monitoring architectural evolution"
        };
    }

    private static string GetCouplingAssessment(double couplingStrength)
    {
        return couplingStrength switch
        {
            >= 0.8 => "Very High - Critical coupling",
            >= 0.6 => "High - Significant coupling",
            >= 0.4 => "Moderate - Some coupling",
            >= 0.2 => "Low - Minimal coupling",
            _ => "Very Low - Weak coupling"
        };
    }

    private static object[] IdentifyCouplingHotspots(List<NamespaceCoupling> couplings)
    {
        // Group by source namespace to find namespaces with many outgoing dependencies
        var sourceHotspots = couplings
            .GroupBy(c => c.SourceNamespace)
            .Where(g => g.Count() >= 3) // At least 3 outgoing dependencies
            .OrderByDescending(g => g.Sum(c => c.CouplingStrength))
            .Take(5)
            .Select(g => new
            {
                namespace_name = g.Key,
                type = "Source Hotspot",
                description = $"Has {g.Count()} outgoing dependencies",
                total_coupling_strength = Math.Round(g.Sum(c => c.CouplingStrength), 2),
                dependencies = g.Select(c => c.TargetNamespace).ToArray()
            }).ToArray();

        // Group by target namespace to find heavily used namespaces
        var targetHotspots = couplings
            .GroupBy(c => c.TargetNamespace)
            .Where(g => g.Count() >= 3) // At least 3 incoming dependencies
            .OrderByDescending(g => g.Sum(c => c.CouplingStrength))
            .Take(5)
            .Select(g => new
            {
                namespace_name = g.Key,
                type = "Target Hotspot",
                description = $"Used by {g.Count()} other namespaces",
                total_coupling_strength = Math.Round(g.Sum(c => c.CouplingStrength), 2),
                dependencies = g.Select(c => c.SourceNamespace).ToArray()
            }).ToArray();

        return sourceHotspots.Concat(targetHotspots).ToArray<object>();
    }

    private static string[] GenerateCouplingRefactoringSuggestions(List<NamespaceCoupling> couplings)
    {
        var suggestions = new List<string>();

        List<NamespaceCoupling> veryHighCouplings = couplings.Where(c => c.CouplingStrength > 0.8).ToList();
        if (veryHighCouplings.Count != 0)
        {
            suggestions.Add($"?? Extract interfaces for {veryHighCouplings.Count} very high coupling relationships");
        }

        List<NamespaceCoupling> crossPlatformCouplings = couplings.Where(c => c.IsCrossPlatformCoupling).ToList();
        if (crossPlatformCouplings.Count != 0)
        {
            suggestions.Add($"?? Introduce adapter pattern for {crossPlatformCouplings.Count} cross-platform dependencies");
        }

        List<NamespaceCoupling> frequentCouplings = couplings.Where(c => c.UsageCount > 10).ToList();
        if (frequentCouplings.Count != 0)
        {
            suggestions.Add($"?? Consider creating shared packages for {frequentCouplings.Count} frequently used dependencies");
        }

        return suggestions.ToArray();
    }

    #endregion
}
