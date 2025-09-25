using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;
using McpCodeEditor.Services.Analysis;
using McpCodeEditor.Services.Refactoring;
using McpCodeEditor.Services.TypeScript;
using McpCodeEditor.Services.FileOperations;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Tools;

/// <summary>
/// TypeScript-specific tool endpoints for analysis and refactoring
/// Provides specialized tools for TypeScript/TSX files
/// </summary>
[McpServerToolType]
public partial class TypeScriptTools(
    TypeScriptAnalysisService analysisService,
    TypeScriptSymbolRenameService symbolRenameService,
    IRefactoringOrchestrator refactoringOrchestrator,
    TypeScriptFileResolver fileResolver,
    TypeScriptFileReader fileReader,
    CodeEditorConfigurationService config)
{
    #region TypeScript Analysis

    [McpServerTool]
    [Description("Analyze TypeScript file structure and extract symbols (functions, classes, interfaces, variables)")]
    public async Task<string> TypeScriptAnalyzeFileAsync(
        [Description("Path to the TypeScript file to analyze")]
        string filePath)
    {
        try
        {
            var result = await analysisService.AnalyzeFileAsync(filePath);
            
            var response = new
            {
                success = result.Success,
                file_path = result.FilePath,
                content_length = result.ContentLength,
                has_syntax_errors = result.HasSyntaxErrors,
                error_message = result.ErrorMessage,
                analysis = new
                {
                    functions = result.Functions.Select(f => new
                    {
                        name = f.Name,
                        start_line = f.StartLine,
                        end_line = f.EndLine,
                        parameters = f.Parameters,
                        return_type = f.ReturnType,
                        is_async = f.IsAsync,
                        is_exported = f.IsExported
                    }).ToArray(),
                    classes = result.Classes.Select(c => new
                    {
                        name = c.Name,
                        start_line = c.StartLine,
                        end_line = c.EndLine,
                        extends_class = c.ExtendsClass,
                        implements_interfaces = c.ImplementsInterfaces,
                        is_exported = c.IsExported,
                        is_abstract = c.IsAbstract
                    }).ToArray(),
                    interfaces = result.Interfaces.Select(i => new
                    {
                        name = i.Name,
                        start_line = i.StartLine,
                        end_line = i.EndLine,
                        extends_interfaces = i.ExtendsInterfaces,
                        is_exported = i.IsExported
                    }).ToArray(),
                    variables = result.Variables.Select(v => new
                    {
                        name = v.Name,
                        type = v.Type,
                        start_line = v.StartLine,
                        is_const = v.IsConst,
                        is_let = v.IsLet,
                        is_exported = v.IsExported
                    }).ToArray(),
                    type_aliases = result.TypeAliases.Select(t => new
                    {
                        name = t.Name,
                        aliased_type = t.AliasedType,
                        start_line = t.StartLine,
                        is_exported = t.IsExported
                    }).ToArray(),
                    imports = result.Imports.Select(i => new
                    {
                        source = i.Source,
                        imported_symbols = i.ImportedSymbols,
                        line = i.Line,
                        is_default = i.IsDefault,
                        is_namespace = i.IsNamespace
                    }).ToArray(),
                    exports = result.Exports.Select(e => new
                    {
                        line = e.Line,
                        is_default = e.IsDefault,
                        exported_symbols = e.ExportedSymbols,
                        source = e.Source
                    }).ToArray()
                }
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [McpServerTool]
    [Description("Find TypeScript symbols by name across the analyzed structure")]
    public async Task<string> TypeScriptFindSymbolAsync(
        [Description("Path to the TypeScript file")]
        string filePath,
        [Description("Symbol name to search for")]
        string symbolName)
    {
        try
        {
            var analysisResult = await analysisService.AnalyzeFileAsync(filePath);
            if (!analysisResult.Success)
            {
                return JsonSerializer.Serialize(new { success = false, error = analysisResult.ErrorMessage },
                    new JsonSerializerOptions { WriteIndented = true });
            }

            var symbols = TypeScriptAnalysisService.FindSymbolsByName(analysisResult, symbolName);
            
            var response = new
            {
                success = true,
                file_path = filePath,
                symbol_name = symbolName,
                found_count = symbols.Count,
                symbols = symbols.Select(s => new
                {
                    name = s.Name,
                    type = s.Type,
                    start_line = s.StartLine,
                    end_line = s.EndLine,
                    start_column = s.StartColumn,
                    end_column = s.EndColumn
                }).ToArray()
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region TypeScript Refactoring

    [McpServerTool]
    [Description("Rename a TypeScript symbol across files with intelligent pattern matching")]
    public async Task<string> TypeScriptRenameSymbolAsync(
        [Description("Current symbol name to rename")]
        string symbolName,
        [Description("New name for the symbol")]
        string newName,
        [Description("Optional file path to limit search scope")]
        string? filePath = null,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        try
        {
            var result = await symbolRenameService.RenameSymbolAsync(symbolName, newName, filePath, previewOnly);
            
            var response = new
            {
                success = result.Success,
                message = result.Message,
                error = result.Error,
                files_affected = result.FilesAffected,
                metadata = result.Metadata,
                changes = result.Changes?.Select(c => new
                {
                    file_path = c.FilePath,
                    change_type = c.ChangeType,
                    line_changes_count = c.LineChanges.Count,
                    sample_changes = c.LineChanges.Take(3).Select(lc => new
                    {
                        line_number = lc.LineNumber,
                        original = lc.Original?.Length > 100 ? lc.Original[..100] + "..." : lc.Original,
                        modified = lc.Modified?.Length > 100 ? lc.Modified[..100] + "..." : lc.Modified,
                        change_type = lc.ChangeType
                    }).ToArray()
                }).ToArray()
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// TECH-007 Slice 10: Updated to use new RefactoringOrchestrator for TypeScript method extraction
    /// </summary>
    [McpServerTool]
    [Description("Extract TypeScript method from selected lines with comprehensive validation and support for all TypeScript function types (function, arrow, async, class method)")]
    public async Task<string> RefactorExtractTypeScriptMethodAsync(
        [Description("Path to the TypeScript file")]
        string filePath,
        [Description("Starting line number (1-based)")]
        int startLine,
        [Description("Ending line number (1-based)")]
        int endLine,
        [Description("Name for the new method")]
        string methodName,
        [Description("TypeScript function type: Function, ArrowFunction, Method, AsyncFunction, AsyncArrowFunction")]
        string functionType = "Function",
        [Description("Access modifier (for class methods): private, public, protected")]
        string? accessModifier = "private",
        [Description("Whether the method should be static")]
        bool isStatic = false,
        [Description("Whether the method should be async")]
        bool isAsync = false,
        [Description("Return type annotation (optional)")]
        string? returnType = null,
        [Description("Whether to export the method")]
        bool exportMethod = false,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        try
        {
            // Use the new RefactoringOrchestrator for TypeScript method extraction
            // The orchestrator will automatically detect TypeScript files and delegate to the appropriate service
            var result = await refactoringOrchestrator.ExtractMethodAsync(
                filePath, methodName, startLine, endLine, previewOnly);
            
            var response = new
            {
                success = result.Success,
                message = result.Message,
                error = result.Error,
                files_affected = result.FilesAffected,
                metadata = result.Metadata,
                changes = result.Changes?.Select(c => new
                {
                    file_path = c.FilePath,
                    change_type = c.ChangeType,
                    original_content_preview = c.OriginalContent?.Length > 200 ? c.OriginalContent[..200] + "..." : c.OriginalContent,
                    modified_content_preview = c.ModifiedContent?.Length > 200 ? c.ModifiedContent[..200] + "..." : c.ModifiedContent
                }).ToArray(),
                extraction_details = new
                {
                    method_name = methodName,
                    function_type = functionType,
                    start_line = startLine,
                    end_line = endLine,
                    lines_extracted = endLine - startLine + 1,
                    is_async = isAsync,
                    is_static = isStatic,
                    export_method = exportMethod,
                    return_type = returnType ?? "inferred",
                    access_modifier = accessModifier ?? "default",
                    architecture_note = "Using new SOLID-compliant RefactoringOrchestrator (TECH-007 Slice 10)"
                }
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message },
                new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region TypeScript Project Analysis

    [McpServerTool]
    [Description("Discover and analyze TypeScript projects with intelligent path resolution and project detection")]
    public async Task<string> TypeScriptProjectOverviewAsync(
        [Description("Optional base directory to search for TypeScript projects (if not provided, searches common development locations)")]
        string? baseSearchPath = null)
    {
        try
        {
            // TS-004: Use TypeScriptFileResolver for intelligent project discovery
            var discoveredProjects = await fileResolver.DiscoverTypeScriptProjectsAsync(baseSearchPath);
            
            if (discoveredProjects.Count == 0)
            {
                return JsonSerializer.Serialize(new 
                { 
                    success = true,
                    message = "No TypeScript projects found in search locations",
                    search_path = baseSearchPath ?? "common development directories",
                    discovered_projects = 0,
                    recommendations = new[]
                    {
                        "Ensure TypeScript projects have tsconfig.json or package.json files",
                        "Check that projects are in standard development directories",
                        "Provide a specific baseSearchPath if projects are in custom locations"
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Analyze a sample of projects for overview
            var projectAnalysis = new List<object>();
            var maxProjectsToAnalyze = Math.Min(discoveredProjects.Count, 5); // Limit for performance

            foreach (var project in discoveredProjects.Take(maxProjectsToAnalyze))
            {
                try
                {
                    // Get TypeScript files for this project
                    var fileDiscovery = await fileResolver.FindTypeScriptFilesAsync(project.ProjectPath);
                    
                    if (fileDiscovery is { Success: true, SourceFiles.Count: > 0 })
                    {
                        // Analyze a sample of source files
                        var sampleFiles = fileDiscovery.SourceFiles.Take(10).ToList();
                        var analysisResults = new List<object>();

                        foreach (var file in sampleFiles)
                        {
                            try
                            {
                                var analysis = await analysisService.AnalyzeFileAsync(file);
                                if (analysis.Success)
                                {
                                    analysisResults.Add(new
                                    {
                                        file_path = Path.GetRelativePath(project.ProjectPath, file),
                                        functions_count = analysis.Functions.Count,
                                        classes_count = analysis.Classes.Count,
                                        interfaces_count = analysis.Interfaces.Count,
                                        imports_count = analysis.Imports.Count,
                                        has_syntax_errors = analysis.HasSyntaxErrors
                                    });
                                }
                            }
                            catch
                            {
                                // Skip problematic files
                                continue;
                            }
                        }

                        projectAnalysis.Add(new
                        {
                            project_name = project.ProjectName,
                            project_path = project.ProjectPath,
                            project_type = project.IsAngularProject ? "Angular" : 
                                          project.IsReactProject ? "React" : "TypeScript",
                            has_tsconfig = project.HasTsConfig,
                            has_package_json = project.HasPackageJson,
                            total_source_files = fileDiscovery.SourceFiles.Count,
                            total_test_files = fileDiscovery.TestFiles.Count,
                            total_declaration_files = fileDiscovery.DeclarationFiles.Count,
                            analyzed_sample_files = analysisResults.Count,
                            sample_file_analysis = analysisResults.ToArray(),
                            project_statistics = new
                            {
                                total_functions = analysisResults.Sum(r => (int)((dynamic)r).functions_count),
                                total_classes = analysisResults.Sum(r => (int)((dynamic)r).classes_count),
                                total_interfaces = analysisResults.Sum(r => (int)((dynamic)r).interfaces_count),
                                files_with_errors = analysisResults.Count(r => (bool)((dynamic)r).has_syntax_errors)
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log but continue with other projects
                    projectAnalysis.Add(new
                    {
                        project_name = project.ProjectName,
                        project_path = project.ProjectPath,
                        error = $"Analysis failed: {ex.Message}"
                    });
                }
            }

            var response = new
            {
                success = true,
                ts_004_infrastructure = "Using TypeScriptFileResolver and TypeScriptFileReader for intelligent project discovery",
                search_summary = new
                {
                    search_path = baseSearchPath ?? "common development directories",
                    projects_discovered = discoveredProjects.Count,
                    projects_analyzed = projectAnalysis.Count,
                    analysis_limit = maxProjectsToAnalyze
                },
                discovered_projects = discoveredProjects.Select(p => new
                {
                    project_name = p.ProjectName,
                    project_path = p.ProjectPath,
                    project_type = p.IsAngularProject ? "Angular" : 
                                  p.IsReactProject ? "React" : "TypeScript",
                    source_files = p.SourceFileCount,
                    test_files = p.TestFileCount,
                    declaration_files = p.DeclarationFileCount,
                    has_tsconfig = p.HasTsConfig,
                    has_package_json = p.HasPackageJson,
                    discovered_at = p.DiscoveredAt
                }).ToArray(),
                project_analysis = projectAnalysis.ToArray(),
                overall_statistics = new
                {
                    total_projects = discoveredProjects.Count,
                    angular_projects = discoveredProjects.Count(p => p.IsAngularProject),
                    react_projects = discoveredProjects.Count(p => p.IsReactProject),
                    generic_typescript = discoveredProjects.Count(p => p is { IsAngularProject: false, IsReactProject: false }),
                    total_source_files = discoveredProjects.Sum(p => p.SourceFileCount),
                    total_test_files = discoveredProjects.Sum(p => p.TestFileCount)
                },
                capabilities = new
                {
                    path_resolution = "Intelligent path resolution with TypeScriptFileResolver",
                    project_detection = "Auto-detection of Angular, React, and generic TypeScript projects",
                    cross_project_analysis = "Can analyze projects outside current workspace",
                    architecture_note = "TS-004: Enhanced TypeScript file reading infrastructure"
                }
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { 
                success = false, 
                error = ex.Message,
                ts_004_note = "Error using new TypeScript file reading infrastructure"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Helper Methods

    private bool IsExcludedDirectory(string path)
    {
        var dirName = Path.GetFileName(path);
        return config.ExcludedDirectories.Contains(dirName);
    }

    #endregion
}
