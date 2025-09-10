using McpCodeEditor.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Registration module for refactoring strategy services
/// Part of Phase 3 refactoring - Strategy Pattern implementation
/// </summary>
public static class StrategiesModule
{
    /// <summary>
    /// Register all refactoring strategy services with the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddStrategies(this IServiceCollection services)
    {
        // Register language-specific refactoring strategies
        // FIXED: Changed from Scoped to Singleton for consistency with RefactoringOrchestrator
        services.AddSingleton<ILanguageRefactoringStrategy, CSharpRefactoringStrategy>();
        services.AddSingleton<ILanguageRefactoringStrategy, TypeScriptRefactoringStrategy>();
        
        // Register strategy factory for resolving strategies by language type
        services.AddSingleton<ILanguageRefactoringStrategyFactory, LanguageRefactoringStrategyFactory>();
        
        return services;
    }
}
