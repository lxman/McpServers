using McpCodeEditor.Models;
using McpCodeEditor.Services;

namespace McpCodeEditor.Tools.Common;

/// <summary>
/// Base class providing shared utilities and helper methods for architecture analysis tools.
/// Focused responsibility: Common utilities, validation, and helper methods.
/// Part of TECH-008 SOLID compliance refactoring.
/// </summary>
public abstract class ArchitectureToolsBase(CodeEditorConfigurationService config)
{
    protected readonly CodeEditorConfigurationService ConfigService = config;

    #region Directory and Path Utilities

    /// <summary>
    /// Determines if a directory should be excluded from analysis.
    /// </summary>
    protected static bool IsExcludedDirectory(string directory)
    {
        string dirName = Path.GetFileName(directory).ToLowerInvariant();
        var excludedDirs = new[] 
        { 
            ".git", ".vs", ".vscode", "bin", "obj", "node_modules", 
            "packages", "target", "dist", "build", ".angular", 
            ".nuget", "TestResults", "coverage", "logs"
        };
        return excludedDirs.Contains(dirName);
    }

    /// <summary>
    /// Validates that a directory exists and returns an appropriate error response if not.
    /// </summary>
    protected static object? ValidateDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return new
            {
                success = false,
                error = $"Directory does not exist: {directoryPath}"
            };
        }
        return null;
    }

    /// <summary>
    /// Gets the workspace path to analyze, using the provided path or falling back to default workspace.
    /// </summary>
    protected string GetWorkspacePath(string? providedPath)
    {
        return string.IsNullOrEmpty(providedPath) ? ConfigService.DefaultWorkspace : providedPath;
    }

    #endregion

    #region Assessment and Scoring Utilities

    /// <summary>
    /// Provides a human-readable assessment of isolation scores.
    /// </summary>
    protected static string GetIsolationAssessment(double isolationScore)
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

    /// <summary>
    /// Provides a human-readable assessment of coupling strength.
    /// </summary>
    protected static string GetCouplingAssessment(double couplingStrength)
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

    /// <summary>
    /// Assesses the complexity of detected architecture patterns.
    /// </summary>
    protected static string GetComplexityAssessment(List<ArchitecturePattern> patterns)
    {
        if (patterns.Count == 0) return "Simple - No complex patterns detected";
        if (patterns.Count == 1) return "Low - Single architecture pattern";
        if (patterns.Count <= 3) return "Moderate - Multiple related patterns";
        return "High - Complex multi-pattern architecture";
    }

    #endregion

    #region Technology and Compatibility Analysis

    /// <summary>
    /// Determines if architecture types are complementary (e.g., frontend + backend).
    /// </summary>
    protected static bool IsComplementaryArchitecture(
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
    /// Calculates shared technology overlap between two sets.
    /// </summary>
    protected static double CalculateTechnologyOverlap(HashSet<string> tech1, HashSet<string> tech2)
    {
        int sharedTech = tech1.Intersect(tech2).Count();
        int totalTech = tech1.Union(tech2).Count();
        
        return totalTech > 0 ? (double)sharedTech / totalTech : 0.0;
    }

    #endregion

    #region Naming and Similarity Analysis

    /// <summary>
    /// Calculates naming similarity between two project names.
    /// </summary>
    protected static double CalculateNamingSimilarity(string name1, string name2)
    {
        string normalized1 = name1.ToLowerInvariant();
        string normalized2 = name2.ToLowerInvariant();

        if (normalized1.Contains(normalized2) || normalized2.Contains(normalized1))
            return 0.3; // 30% bonus for name similarity

        return 0.0;
    }

    #endregion

    #region Data Transformation Utilities

    /// <summary>
    /// Rounds a double value to a specified number of decimal places.
    /// </summary>
    protected static double RoundToDecimalPlaces(double value, int decimalPlaces = 3)
    {
        return Math.Round(value, decimalPlaces);
    }

    /// <summary>
    /// Limits a value between 0.0 and 1.0.
    /// </summary>
    protected static double ClampToUnitRange(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }

    /// <summary>
    /// Takes the first N items from a collection, useful for limiting large result sets.
    /// </summary>
    protected static T[] TakeSample<T>(IEnumerable<T> collection, int count)
    {
        return collection.Take(count).ToArray();
    }

    #endregion

    #region Recommendation Generation Utilities

    /// <summary>
    /// Generates recommendations based on architecture patterns.
    /// </summary>
    protected static string[] GeneratePatternBasedRecommendations(List<ArchitecturePattern> patterns)
    {
        var recommendations = new List<string>();

        if (patterns.Any(p => p.Type == ArchitectureType.AngularDotNetApi))
        {
            recommendations.Add("Consider using shared TypeScript models between Angular frontend and .NET API");
            recommendations.Add("Implement proper CORS configuration for development and production");
            recommendations.Add("Set up automated API client generation from OpenAPI/Swagger specs");
        }

        if (patterns.Any(p => p.Type == ArchitectureType.MonoRepoMultiProject))
        {
            recommendations.Add("Consider implementing a build orchestration system (e.g., Nx, Lerna)");
            recommendations.Add("Establish shared configuration and tooling across projects");
            recommendations.Add("Implement consistent code quality standards across all projects");
        }

        if (patterns.Any(p => p.Type == ArchitectureType.McpServerClient))
        {
            recommendations.Add("Ensure proper MCP protocol compliance and error handling");
            recommendations.Add("Consider implementing client connection pooling and retry logic");
        }

        return recommendations.ToArray();
    }

    /// <summary>
    /// Generates architectural strategy recommendation based on detected strategy.
    /// </summary>
    protected static string GetArchitecturalRecommendation(string strategy)
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

    #endregion

    #region Error Handling Utilities

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    protected static object CreateErrorResponse(string errorMessage)
    {
        return new
        {
            success = false,
            error = errorMessage,
            timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a standardized success response with data.
    /// </summary>
    protected static object CreateSuccessResponse(object data)
    {
        return new
        {
            success = true,
            data = data,
            timestamp = DateTime.UtcNow
        };
    }

    #endregion

    #region Abstract Methods for Derived Classes

    /// <summary>
    /// Abstract method that derived classes must implement for tool-specific validation.
    /// </summary>
    protected abstract Task<bool> ValidateToolSpecificRequirementsAsync(string workspacePath);

    #endregion
}
