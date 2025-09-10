using McpCodeEditor.Interfaces;
using McpCodeEditor.Services;
using McpCodeEditor.Services.Analysis;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Registration module for architecture analysis and pattern detection services
/// Extracted from Program.cs to improve maintainability and organization
/// </summary>
public static class ArchitectureServicesModule
{
    /// <summary>
    /// Register all architecture services with the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddArchitectureServices(this IServiceCollection services)
    {
        // Project discovery services (Phase 4 - Service Layer Cleanup)
        services.AddScoped<IProjectDiscoveryService, ProjectDiscoveryService>();
        services.AddScoped<IPatternDetectionStrategyService, PatternDetectionStrategyService>();
        
        // Architecture pattern services
        services.AddSingleton<ArchitecturePatternTemplateService>();
        services.AddSingleton<ArchitectureDetectionService>();
        services.AddSingleton<ArchitectureRecommendationService>();
        
        // Relationship and dependency analysis
        services.AddSingleton<RelationshipScoringService>();
        services.AddSingleton<ProjectReferenceAnalyzer>();
        services.AddSingleton<NamespaceDependencyAnalyzer>();
        
        return services;
    }
}
