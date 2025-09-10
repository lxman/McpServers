using McpCodeEditor.Services;
using McpCodeEditor.Services.BatchOperations;
using McpCodeEditor.Services.CodeGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Registration module for specialized services (code generation, batch operations, search, etc.)
/// Extracted from Program.cs to improve maintainability and organization
/// </summary>
public static class SpecializedServicesModule
{
    /// <summary>
    /// Register all specialized services with the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSpecializedServices(this IServiceCollection services)
    {
        // Code generation services
        services.AddSingleton<ConstructorGenerator>();
        services.AddSingleton<CodeGenerationService>();
        
        // Batch operation services
        services.AddSingleton<BatchReplaceService>();
        services.AddSingleton<BulkFormatService>();
        services.AddSingleton<BatchOperationService>();
        
        // Search and navigation services
        services.AddSingleton<SearchService>();
        services.AddSingleton<SymbolNavigationService>();
        
        // Context analysis services
        services.AddSingleton<ContextAnalysisService>();
        services.AddSingleton<SuggestionRationaleService>();
        
        // Mass operations service
        services.AddSingleton<MassRenameService>();
        
        return services;
    }
}
