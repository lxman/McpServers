using McpCodeEditor.Tools;
using McpCodeEditor.Tools.Advanced;
using McpCodeEditor.Tools.Architecture;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.ServiceModules;

/// <summary>
/// Registration module for MCP tool endpoints
/// Extracted from Program.cs to improve maintainability and organization
/// </summary>
public static class ToolsModule
{
    /// <summary>
    /// Register all MCP tools with the server
    /// </summary>
    /// <param name="builder">The MCP server builder</param>
    /// <returns>The server builder for chaining</returns>
    public static IMcpServerBuilder AddAllTools(this IMcpServerBuilder builder)
    {
        return builder
            // Core refactoring tools
            .WithTools<CoreEditorTools>()
            .WithTools<CSharpRefactoringTools>()
            .WithTools<TypeScriptRefactoringTools>()
            .WithTools<AngularTools>()
            
            // Advanced file reading tools - Roslyn-powered precision reading
            .WithTools<AdvancedFileReaderTools>()
            
            // Analysis and workspace tools
            .WithTools<ProjectAnalysisTools>()
            .WithTools<WorkspaceTools>()
            .WithTools<ContextAnalysisTools>()
            .WithTools<ProjectArchitectureTools>()
            .WithTools<NamespaceAnalysisTools>()
            
            // File and code operation tools
            .WithTools<FileOperationTools>()
            .WithTools<TypeScriptTools>()
            .WithTools<CodeAnalysisTools>()
            .WithTools<CodeGenerationTools>()
            
            // Utility tools
            .WithTools<DiffTools>()
            .WithTools<BackupTools>()
            .WithTools<GitTools>()
            .WithTools<ChangeTrackingTools>()
            
            // Search and navigation tools
            .WithTools<SearchTools>()
            .WithTools<NavigationTools>()
            
            // Batch operation tools
            .WithTools<BatchOperationTools>()
            
            // Diagnostic tools
            .WithTools<DiagnosticTools>();
    }
}
