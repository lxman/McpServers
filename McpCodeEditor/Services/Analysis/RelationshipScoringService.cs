using McpCodeEditor.Models;

namespace McpCodeEditor.Services.Analysis;

/// <summary>
/// Service for calculating relationship scores between projects and architectures.
/// Extracted from ProjectArchitectureTools.cs as part of RS-004 refactoring.
/// Follows SOLID principles with single responsibility for relationship analysis.
/// </summary>
public class RelationshipScoringService(CodeEditorConfigurationService config)
{
    #region Relationship Scoring

    /// <summary>
    /// Calculate relationship score between current patterns and a sibling project
    /// </summary>
    /// <param name="currentPatterns">Architecture patterns in current workspace</param>
    /// <param name="siblingPatterns">Architecture patterns in sibling project</param>
    /// <param name="siblingProject">Project information for sibling</param>
    /// <returns>Relationship score between 0.0 and 1.0</returns>
    public double CalculateRelationshipScore(
        List<ArchitecturePattern> currentPatterns, 
        List<ArchitecturePattern> siblingPatterns, 
        ProjectInfo siblingProject)
    {
        var score = 0.0;

        // Score based on shared technologies
        HashSet<string> currentTech = currentPatterns.SelectMany(p => p.Technologies ?? []).ToHashSet();
        HashSet<string> siblingTech = siblingPatterns.SelectMany(p => p.Technologies ?? [])
            .Concat(siblingProject.Indicators).ToHashSet();

        int sharedTech = currentTech.Intersect(siblingTech).Count();
        int totalTech = currentTech.Union(siblingTech).Count();

        if (totalTech > 0)
        {
            score += (double)sharedTech / totalTech * 0.4; // 40% weight for shared tech
        }

        // Score based on complementary patterns (e.g., frontend + backend)
        HashSet<ArchitectureType> currentTypes = currentPatterns.Select(p => p.Type).ToHashSet();
        HashSet<ArchitectureType> siblingTypes = siblingPatterns.Select(p => p.Type).ToHashSet();

        if (IsComplementaryArchitecture(currentTypes, siblingTypes, siblingProject.Type))
        {
            score += 0.5; // 50% bonus for complementary architectures
        }

        // Score based on naming similarity
        string currentName = Path.GetFileName(config.DefaultWorkspace).ToLowerInvariant();
        string siblingName = siblingProject.Name.ToLowerInvariant();

        if (siblingName.Contains(currentName) || currentName.Contains(siblingName))
        {
            score += 0.3; // 30% bonus for name similarity
        }

        return Math.Min(1.0, score);
    }

    /// <summary>
    /// Check if two sets of architecture types are complementary
    /// </summary>
    /// <param name="currentTypes">Current architecture types</param>
    /// <param name="siblingTypes">Sibling architecture types</param>
    /// <param name="siblingProjectType">Type of the sibling project</param>
    /// <returns>True if architectures are complementary</returns>
    public static bool IsComplementaryArchitecture(
        HashSet<ArchitectureType> currentTypes, 
        HashSet<ArchitectureType> siblingTypes, 
        ProjectType siblingProjectType)
    {
        // Frontend + Backend combinations
        if (currentTypes.Contains(ArchitectureType.AngularDotNetApi) &&
            siblingProjectType is ProjectType.DotNet or ProjectType.Angular)
            return true;

        if (currentTypes.Contains(ArchitectureType.ReactNodeJsDatabase) &&
            siblingProjectType is ProjectType.React or ProjectType.NodeJs)
            return true;

        // MCP Server/Client combinations
        if (currentTypes.Contains(ArchitectureType.McpServerClient))
            return true;

        return false;
    }

    /// <summary>
    /// Generate human-readable reasons for the relationship between projects
    /// </summary>
    /// <param name="currentPatterns">Current project's architecture patterns</param>
    /// <param name="siblingPatterns">Sibling project's architecture patterns</param>
    /// <param name="siblingProject">Sibling project information</param>
    /// <returns>Array of relationship reason strings</returns>
    public static string[] GenerateRelationshipReasons(
        List<ArchitecturePattern> currentPatterns, 
        List<ArchitecturePattern> siblingPatterns, 
        ProjectInfo siblingProject)
    {
        var reasons = new List<string>();

        HashSet<string> currentTech = currentPatterns.SelectMany(p => p.Technologies ?? []).ToHashSet();
        HashSet<string> siblingTech = siblingPatterns.SelectMany(p => p.Technologies ?? [])
            .Concat(siblingProject.Indicators).ToHashSet();
        IEnumerable<string> sharedTech = currentTech.Intersect(siblingTech);

        List<string> sharedTechList = sharedTech.ToList();
        if (sharedTechList.Count != 0)
        {
            reasons.Add($"Shares technologies: {string.Join(", ", sharedTechList)}");
        }

        HashSet<ArchitectureType> currentTypes = currentPatterns.Select(p => p.Type).ToHashSet();
        HashSet<ArchitectureType> siblingTypes = siblingPatterns.Select(p => p.Type).ToHashSet();

        if (IsComplementaryArchitecture(currentTypes, siblingTypes, siblingProject.Type))
        {
            reasons.Add($"Complementary architecture: {siblingProject.Type}");
        }

        if (siblingPatterns.Count != 0)
        {
            reasons.Add($"Detected patterns: {string.Join(", ", siblingPatterns.Select(p => p.Name))}");
        }

        return reasons.ToArray();
    }

    #endregion

    #region Analysis Utilities

    /// <summary>
    /// Get shared technologies between two architecture pattern lists
    /// </summary>
    /// <param name="patterns1">First set of patterns</param>
    /// <param name="patterns2">Second set of patterns</param>
    /// <returns>Set of shared technologies</returns>
    public static HashSet<string> GetSharedTechnologies(
        List<ArchitecturePattern> patterns1, 
        List<ArchitecturePattern> patterns2)
    {
        HashSet<string> tech1 = patterns1.SelectMany(p => p.Technologies ?? []).ToHashSet();
        HashSet<string> tech2 = patterns2.SelectMany(p => p.Technologies ?? []).ToHashSet();

        return tech1.Intersect(tech2).ToHashSet();
    }

    /// <summary>
    /// Calculate the technology similarity ratio between two pattern sets
    /// </summary>
    /// <param name="patterns1">First set of patterns</param>
    /// <param name="patterns2">Second set of patterns</param>
    /// <returns>Similarity ratio between 0.0 and 1.0</returns>
    public static double CalculateTechnologySimilarity(
        List<ArchitecturePattern> patterns1, 
        List<ArchitecturePattern> patterns2)
    {
        HashSet<string> tech1 = patterns1.SelectMany(p => p.Technologies ?? []).ToHashSet();
        HashSet<string> tech2 = patterns2.SelectMany(p => p.Technologies ?? []).ToHashSet();

        int sharedCount = tech1.Intersect(tech2).Count();
        int totalCount = tech1.Union(tech2).Count();

        return totalCount > 0 ? (double)sharedCount / totalCount : 0.0;
    }

    #endregion
}
