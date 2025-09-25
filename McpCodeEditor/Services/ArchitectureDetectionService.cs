using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for detecting multi-platform architecture patterns and project relationships
/// Phase 4 Refactored: Now uses focused injected services for all major responsibilities
/// </summary>
public class ArchitectureDetectionService(
    IProjectDiscoveryService projectDiscovery,
    IPatternDetectionStrategyService patternDetectionStrategy,
    ArchitecturePatternTemplateService templateService)
{
    /// <summary>
    /// Analyze a directory for architecture patterns
    /// Now acts as a lightweight orchestrator using focused services
    /// </summary>
    public async Task<List<ArchitecturePattern>> DetectArchitecturePatternsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var detectedPatterns = new List<ArchitecturePattern>();

        if (!Directory.Exists(rootPath))
        {
            return detectedPatterns;
        }

        try
        {
            // Use injected project discovery service
            var projects = await projectDiscovery.GetProjectsInDirectoryAsync(rootPath, cancellationToken);

            // Debug: Log what projects were found
            Console.WriteLine($"Found {projects.Count} projects:");
            foreach (var proj in projects)
            {
                Console.WriteLine($"  - {proj.Name} ({proj.Type}) at {proj.Path}");
            }

            // Use injected pattern detection strategy service for all detection strategies
            var solutionPatterns = await patternDetectionStrategy.DetectSolutionBasedPatternsAsync(rootPath, projects, cancellationToken);
            detectedPatterns.AddRange(solutionPatterns);

            var directoryPatterns = await patternDetectionStrategy.DetectDirectoryBasedPatternsAsync(rootPath, projects, cancellationToken);
            detectedPatterns.AddRange(directoryPatterns);

            var namingPatterns = await patternDetectionStrategy.DetectNamingBasedPatternsAsync(rootPath, projects, cancellationToken);
            detectedPatterns.AddRange(namingPatterns);

            var combinationPatterns = await patternDetectionStrategy.DetectProjectCombinationPatternsAsync(rootPath, projects, cancellationToken);
            detectedPatterns.AddRange(combinationPatterns);

            // Remove duplicates and merge similar patterns
            detectedPatterns = MergeAndDeduplicatePatterns(detectedPatterns);

            // Calculate final confidence scores
            foreach (var pattern in detectedPatterns)
            {
                pattern.ConfidenceScore = CalculatePatternConfidence(pattern);
            }

            // Lower minimum confidence threshold to catch more patterns
            return detectedPatterns.Where(p => p.ConfidenceScore >= 0.3).ToList();
        }
        catch (Exception ex)
        {
            // Log error but don't fail completely
            Console.WriteLine($"Error in architecture detection: {ex.Message}");
            return detectedPatterns;
        }
    }

    /// <summary>
    /// Merge and deduplicate patterns to avoid duplicates
    /// </summary>
    private static List<ArchitecturePattern> MergeAndDeduplicatePatterns(List<ArchitecturePattern> patterns)
    {
        // Simple deduplication by type and root path
        return patterns
            .GroupBy(p => new { p.Type, p.RootPath })
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Calculate confidence score for a detected pattern
    /// </summary>
    private static double CalculatePatternConfidence(ArchitecturePattern pattern)
    {
        // Better confidence calculation
        if (!pattern.Indicators?.Any() == true) 
        {
            // If no indicators, base confidence on detection reasons and project count
            var baseConfidence = 0.5;
            if (pattern.DetectionReasons?.Count > 0) baseConfidence += 0.2;
            if (pattern.ProjectPaths?.Count > 1) baseConfidence += 0.2;
            return Math.Min(1.0, baseConfidence);
        }

        var totalWeight = pattern.Indicators.Sum(i => i.Weight);
        var maxPossibleWeight = pattern.Indicators.Count * 2.0; // Assuming max weight is 2.0 per indicator

        var calculatedConfidence = Math.Min(1.0, totalWeight / maxPossibleWeight);
        
        // Boost confidence for strong pattern matches
        if (pattern.ProjectPaths?.Count >= 2) calculatedConfidence += 0.1;
        if (pattern.DetectionReasons?.Count >= 2) calculatedConfidence += 0.1;
        
        return Math.Min(1.0, calculatedConfidence);
    }

    /// <summary>
    /// Get minimum confidence threshold for a pattern type
    /// </summary>
    private double GetMinConfidenceThreshold(ArchitectureType type)
    {
        return templateService.GetMinConfidenceThreshold(type);
    }
}
