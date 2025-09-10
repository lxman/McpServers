using McpCodeEditor.Interfaces;
using McpCodeEditor.Services.CSharp;
using McpCodeEditor.Services.Refactoring;
using McpCodeEditor.Services.Refactoring.Angular;
using McpCodeEditor.Services.Validation;
using McpCodeEditor.Services.Refactoring.CSharp;
using McpCodeEditor.Services.Refactoring.TypeScript;
using McpCodeEditor.Services.TypeScript;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Service module for refactoring-related services
/// Fixed DI issues by adding missing service registrations
/// SESSION 2 FIX: Added ParameterFilteringService registration
/// </summary>
public static class RefactoringServicesModule
{
    /// <summary>
    /// Adds refactoring services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddRefactoringServices(this IServiceCollection services)
    {
        // Language detection service - CRITICAL: Was missing
        services.AddSingleton<LanguageDetectionService>();
        
        // Symbol rename service - CRITICAL: Was missing  
        services.AddSingleton<SymbolRenameService>();
        
        // TypeScript Symbol rename service - CRITICAL FIX #4: Was missing!
        services.AddSingleton<TypeScriptSymbolRenameService>();
        
        // Refactoring orchestrator - uses strategies and commands (Phase 3)
        services.AddSingleton<IRefactoringOrchestrator, RefactoringOrchestrator>();
        
        // TypeScript refactoring service
        services.AddSingleton<ITypeScriptRefactoringService, TypeScriptRefactoringService>();
        
        // Validation services - FIXED: ExtractMethodValidator now properly gets IEnhancedVariableAnalysisService injected
        services.AddSingleton<ExtractMethodValidator>(provider =>
        {
            var enhancedVariableAnalysis = provider.GetService<IEnhancedVariableAnalysisService>();
            return new ExtractMethodValidator(enhancedVariableAnalysis);
        });
        services.AddSingleton<TypeScriptExtractMethodValidator>();
        services.AddSingleton<ITypeScriptSyntaxValidator, TypeScriptSyntaxValidator>();
        
        // Semantic analyzers and helpers
        services.AddSingleton<SemanticReturnAnalyzer>();
        // Note: VariableTypeInferenceHelper is a static class and doesn't need DI registration
        
        // TypeScript analysis services
        services.AddSingleton<TypeScriptScopeAnalyzer>();
        services.AddSingleton<TypeScriptReturnAnalyzer>();
        
        // TypeScript method extraction - now using AST-based implementation
        services.AddSingleton<TypeScriptMethodExtractor>();
        services.AddSingleton<ExpressionBoundaryDetectionService>();
        
        // TypeScript cross-file operations
        services.AddSingleton<TypeScriptCrossFileRenamer>();
        
        // SESSION 2 FIX: Add ParameterFilteringService for proper parameter filtering
        services.AddSingleton<IParameterFilteringService, ParameterFilteringService>();
        
        // C# Enhanced Variable Analysis Service (for P1-T1 fix implementation)
        services.AddSingleton<IEnhancedVariableAnalysisService, EnhancedVariableAnalysisService>();
        
        // C# method call generation service (extracted from CSharpMethodExtractor)
        services.AddSingleton<IMethodCallGenerationService, MethodCallGenerationService>();
        
        // C# method signature generation service (extracted from CSharpMethodExtractor)
        services.AddSingleton<IMethodSignatureGenerationService, MethodSignatureGenerationService>();
        
        // C# code modification service (extracted from CSharpMethodExtractor)
        services.AddSingleton<ICodeModificationService, CodeModificationService>();
        
        // C# Services
        services.AddSingleton<ICSharpMethodExtractor, CSharpMethodExtractor>();
        services.AddSingleton<ICSharpImportManager, CSharpImportManager>();
        services.AddSingleton<ICSharpVariableOperations, CSharpVariableOperations>();
        services.AddSingleton<ICSharpMethodInliner, CSharpMethodInliner>();
        
        // TypeScript Services
        services.AddSingleton<ITypeScriptImportManager, TypeScriptImportManager>();
        services.AddSingleton<ITypeScriptVariableOperations, TypeScriptVariableOperations>();
        services.AddSingleton<ITypeScriptMethodInliner, TypeScriptMethodInliner>();
        
        // Angular services
        services.AddSingleton<AngularComponentRefactorer>();
        
        return services;
    }
}
