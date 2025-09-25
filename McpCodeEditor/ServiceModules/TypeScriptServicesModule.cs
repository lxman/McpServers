using McpCodeEditor.Services.Analysis;
using McpCodeEditor.Services.FileOperations;
using McpCodeEditor.Services.TypeScript;
using McpCodeEditor.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Jering.Javascript.NodeJS;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Registration module for TypeScript-specific analysis and processing services
/// Extracted from Program.cs to improve maintainability and organization
/// Note: TypeScript refactoring services are registered in RefactoringServicesModule
/// </summary>
public static class TypeScriptServicesModule
{
    /// <summary>
    /// Register all TypeScript analysis services with the DI container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddTypeScriptServices(this IServiceCollection services)
    {
        // Node.js integration for TypeScript compiler access
        // IMPORTANT: Requires Node.js to be installed on the system
        
        // Configure NodeJS process options
        services.Configure<NodeJSProcessOptions>(options =>
        {
            // Set the working directory to where the package.json and node_modules are located
            options.ProjectPath = AppContext.BaseDirectory;
            
            // Add the node_modules path to NODE_PATH environment variable
            var nodeModulesPath = Path.Combine(AppContext.BaseDirectory, "node_modules");
            if (Directory.Exists(nodeModulesPath))
            {
                options.EnvironmentVariables["NODE_PATH"] = nodeModulesPath;
            }
            
            // Set Node.js options to increase memory limit
            options.NodeAndV8Options = "--max-old-space-size=4096";
        });
        
        // Add NodeJS service - must be AFTER configuration
        services.AddNodeJS();
        
        // Core TypeScript analysis capabilities
        services.AddSingleton<TypeScriptAnalysisService>();
        
        // Enhanced AST-based TypeScript parsing and context preservation
        services.AddSingleton<TypeScriptAstParserService>();
        services.AddSingleton<TypeScriptContextPreserver>();
        
        // TypeScript project structure and file handling
        services.AddSingleton<TypeScriptProjectAnalyzer>();
        services.AddSingleton<TypeScriptFileResolver>();
        services.AddSingleton<TypeScriptFileReader>();
        
        // Phase 2 - Extracted Services (Phase A2 implementation)
        // FIXED: Changed from Scoped to Singleton to resolve DI lifetime issues
        // These services are injected into Singleton services in RefactoringServicesModule
        services.AddSingleton<IExpressionBoundaryDetectionService, ExpressionBoundaryDetectionService>();
        services.AddSingleton<IVariableDeclarationGeneratorService, VariableDeclarationGeneratorService>(); // Phase A2-T3
        services.AddSingleton<ITypeScriptAstAnalysisService, TypeScriptAstAnalysisService>(); // Phase A2-T2
        services.AddSingleton<ITypeScriptCodeModificationService, TypeScriptCodeModificationService>(); // Phase A2-T4: NEW
        
        return services;
    }
}
