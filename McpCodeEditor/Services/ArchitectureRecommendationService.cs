using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// RS-004: Service for generating architecture recommendations and complexity assessments.
/// Extracted from ProjectArchitectureTools.cs to follow Single Responsibility Principle.
/// </summary>
public class ArchitectureRecommendationService
{
    /// <summary>
    /// Generates general architecture recommendations based on detected patterns and projects
    /// </summary>
    /// <param name="patterns">Detected architecture patterns</param>
    /// <param name="projects">Related projects</param>
    /// <returns>Array of recommendation strings</returns>
    public static string[] GenerateArchitectureRecommendations(
        List<ArchitecturePattern> patterns, 
        List<ProjectInfo> projects)
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

        if (projects.Count > 3)
        {
            recommendations.Add("Consider setting up a centralized solution file to manage all projects");
            recommendations.Add("Implement shared build scripts and CI/CD pipelines");
        }

        return recommendations.ToArray();
    }

    /// <summary>
    /// Assesses the complexity of an architecture based on detected patterns
    /// </summary>
    /// <param name="patterns">Detected architecture patterns</param>
    /// <returns>Complexity assessment string</returns>
    public static string GetComplexityAssessment(List<ArchitecturePattern> patterns)
    {
        if (patterns.Count == 0) return "Simple - No complex patterns detected";

        if (patterns.Count == 1) return "Low - Single architecture pattern";

        if (patterns.Count <= 3) return "Moderate - Multiple related patterns";

        return "High - Complex multi-pattern architecture";
    }

    /// <summary>
    /// Generates detailed architecture recommendations with emojis and specific guidance
    /// </summary>
    /// <param name="patterns">Architecture patterns to analyze</param>
    /// <returns>Array of detailed recommendation strings</returns>
    public static string[] GenerateDetailedArchitectureRecommendations(List<ArchitecturePattern> patterns)
    {
        var recommendations = new List<string>();

        foreach (ArchitecturePattern pattern in patterns.OrderByDescending(p => p.ConfidenceScore))
        {
            switch (pattern.Type)
            {
                case ArchitectureType.AngularDotNetApi:
                    recommendations.Add($"?? {pattern.Name}: Implement DevOps pipeline with Angular build ? .NET API deployment");
                    recommendations.Add($"?? {pattern.Name}: Consider adding Redis for session management and API caching");
                    break;

                case ArchitectureType.ReactNodeJsDatabase:
                    recommendations.Add($"?? {pattern.Name}: Set up containerization with Docker for consistent environments");
                    recommendations.Add($"?? {pattern.Name}: Implement API versioning and proper error handling middleware");
                    break;

                case ArchitectureType.McpServerClient:
                    recommendations.Add($"?? {pattern.Name}: Ensure proper MCP protocol compliance and error handling");
                    recommendations.Add($"?? {pattern.Name}: Consider implementing client connection pooling and retry logic");
                    break;

                case ArchitectureType.MonoRepoMultiProject:
                    recommendations.Add($"?? {pattern.Name}: Implement workspace-wide dependency management and security scanning");
                    recommendations.Add($"?? {pattern.Name}: Set up incremental builds and smart test execution");
                    break;
            }
        }

        return recommendations.ToArray();
    }

    /// <summary>
    /// Generates pattern-specific recommendations based on architecture type
    /// </summary>
    /// <param name="architectureType">Type of architecture pattern</param>
    /// <param name="patternName">Name of the specific pattern</param>
    /// <returns>Array of specific recommendations</returns>
    public static string[] GeneratePatternSpecificRecommendations(ArchitectureType architectureType, string patternName)
    {
        return architectureType switch
        {
            ArchitectureType.AngularDotNetApi =>
            [
                $"?? {patternName}: Use Angular HTTP interceptors for authentication and error handling",
                $"? {patternName}: Implement lazy loading for Angular modules to improve performance",
                $"?? {patternName}: Set up JWT authentication with refresh tokens",
                $"?? {patternName}: Add logging and monitoring with Application Insights or similar"
            ],
            ArchitectureType.ReactNodeJsDatabase =>
            [
                $"?? {patternName}: Implement state management with Redux or Zustand",
                $"? {patternName}: Use React.memo and useMemo for performance optimization",
                $"?? {patternName}: Implement proper input validation on both client and server",
                $"?? {patternName}: Add database connection pooling and query optimization"
            ],
            ArchitectureType.McpServerClient =>
            [
                $"?? {patternName}: Implement comprehensive error handling for MCP protocol",
                $"? {patternName}: Add connection heartbeat and automatic reconnection",
                $"?? {patternName}: Validate all MCP message schemas for security",
                $"?? {patternName}: Add metrics collection for MCP operations"
            ],
            ArchitectureType.MonoRepoMultiProject =>
            [
                $"?? {patternName}: Establish shared linting and formatting configurations",
                $"? {patternName}: Implement incremental builds with dependency caching",
                $"?? {patternName}: Set up security scanning across all projects",
                $"?? {patternName}: Create unified reporting and monitoring dashboard"
            ],
            _ =>
            [
                $"?? {patternName}: Document the architecture pattern and its components",
                $"? {patternName}: Optimize for performance and scalability",
                $"?? {patternName}: Implement proper security measures",
                $"?? {patternName}: Add monitoring and logging capabilities"
            ]
        };
    }

    /// <summary>
    /// Generates modernization recommendations for legacy patterns
    /// </summary>
    /// <param name="patterns">Architecture patterns to analyze</param>
    /// <returns>Array of modernization recommendations</returns>
    public static string[] GenerateModernizationRecommendations(List<ArchitecturePattern> patterns)
    {
        var recommendations = new List<string>();

        foreach (ArchitecturePattern pattern in patterns)
        {
            // Check if the pattern uses older technologies
            List<string> technologies = pattern.Technologies ?? [];
            
            if (technologies.Any(t => t.Contains("jQuery")))
            {
                recommendations.Add($"?? {pattern.Name}: Consider migrating from jQuery to modern framework (React/Vue/Angular)");
            }

            if (technologies.Any(t => t.Contains("ASP.NET") && !t.Contains("Core")))
            {
                recommendations.Add($"?? {pattern.Name}: Consider upgrading from ASP.NET Framework to ASP.NET Core");
            }

            if (technologies.Any(t => t.Contains("Node.js") && pattern.Technologies?.Any(tech => tech.Contains("Express")) == true))
            {
                recommendations.Add($"?? {pattern.Name}: Consider adding TypeScript for better type safety");
            }
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("? Architecture appears to use modern technologies and practices");
        }

        return recommendations.ToArray();
    }
}
