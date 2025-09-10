using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Models.Angular;
using McpCodeEditor.Services.Refactoring.Angular;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools;

/// <summary>
/// MCP tools for Angular component refactoring operations.
/// Provides specialized refactoring capabilities for Angular components, services, and architecture.
/// </summary>
public class AngularTools(AngularComponentRefactorer angularRefactorer) : BaseToolClass
{
    #region Angular Component Analysis

    [McpServerTool]
    [Description("Analyze an Angular component file and extract comprehensive component information")]
    public async Task<string> AnalyzeAngularComponentAsync(
        [Description("Path to the Angular component TypeScript file (.component.ts)")]
        string filePath)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);

            if (!filePath.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be an Angular component (.component.ts file).", nameof(filePath));
            }

            AngularComponentAnalysisResult result = await angularRefactorer.AnalyzeComponentAsync(filePath);
            return result;
        });
    }

    #endregion

    #region Angular Component Refactoring

    [McpServerTool]
    [Description("Extract Angular lifecycle hook logic into separate methods for better organization")]
    public async Task<string> ExtractAngularLifecycleLogicAsync(
        [Description("Path to the Angular component TypeScript file (.component.ts)")]
        string filePath,
        [Description("Lifecycle hook name (e.g., 'ngOnInit', 'ngOnDestroy', 'ngAfterViewInit')")]
        string hookName,
        [Description("Extract template logic from lifecycle hooks")]
        bool extractTemplateLogic = true,
        [Description("Preserve lifecycle hook structure")]
        bool preserveLifecycleHooks = true,
        [Description("Maintain dependency injection context")]
        bool maintainDependencyInjection = true,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);
            ValidateRequiredParameter(hookName, nameof(hookName));

            if (!filePath.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be an Angular component (.component.ts file).", nameof(filePath));
            }

            // Validate lifecycle hook name
            var validHooks = new[] { "ngOnInit", "ngOnDestroy", "ngOnChanges", "ngDoCheck", 
                                   "ngAfterContentInit", "ngAfterContentChecked", 
                                   "ngAfterViewInit", "ngAfterViewChecked" };
            
            if (!validHooks.Contains(hookName))
            {
                throw new ArgumentException($"Invalid lifecycle hook name. Valid hooks: {string.Join(", ", validHooks)}", nameof(hookName));
            }

            var options = new AngularRefactoringOptions
            {
                ExtractTemplateLogic = extractTemplateLogic,
                PreserveLifecycleHooks = preserveLifecycleHooks,
                MaintainDependencyInjection = maintainDependencyInjection
            };

            AngularComponentRefactoringResult result = await angularRefactorer.ExtractLifecycleLogicAsync(
                filePath, hookName, options);
            return result;
        });
    }

    [McpServerTool]
    [Description("Separate business logic from Angular component into a dedicated service")]
    public async Task<string> SeparateAngularBusinessLogicAsync(
        [Description("Path to the Angular component TypeScript file (.component.ts)")]
        string filePath,
        [Description("Separate business logic into service")]
        bool separateBusinessLogic = true,
        [Description("Extract utility methods")]
        bool extractUtilityMethods = true,
        [Description("Update module declarations automatically")]
        bool updateModuleDeclarations = true,
        [Description("Optimize imports")]
        bool optimizeImports = true,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);

            if (!filePath.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be an Angular component (.component.ts file).", nameof(filePath));
            }

            var options = new AngularRefactoringOptions
            {
                SeparateBusinessLogic = separateBusinessLogic,
                ExtractUtilityMethods = extractUtilityMethods,
                UpdateModuleDeclarations = updateModuleDeclarations,
                OptimizeImports = optimizeImports
            };

            AngularComponentRefactoringResult result = await angularRefactorer.SeparateBusinessLogicAsync(
                filePath, options);
            return result;
        });
    }

    [McpServerTool]
    [Description("Optimize Angular component for better change detection performance")]
    public async Task<string> OptimizeAngularChangeDetectionAsync(
        [Description("Path to the Angular component TypeScript file (.component.ts)")]
        string filePath,
        [Description("Optimize change detection strategy")]
        bool optimizeChangeDetection = true,
        [Description("Update template references")]
        bool updateTemplateReferences = true,
        [Description("Maintain event bindings")]
        bool maintainEventBindings = true,
        [Description("Preview only - don't apply changes")]
        bool previewOnly = false)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);

            if (!filePath.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be an Angular component (.component.ts file).", nameof(filePath));
            }

            var options = new AngularRefactoringOptions
            {
                OptimizeChangeDetection = optimizeChangeDetection,
                UpdateTemplateReferences = updateTemplateReferences,
                MaintainEventBindings = maintainEventBindings
            };

            AngularComponentRefactoringResult result = await angularRefactorer.OptimizeChangeDetectionAsync(
                filePath, options);
            return result;
        });
    }

    #endregion

    #region Angular Project Health

    [McpServerTool]
    [Description("Get comprehensive recommendations for Angular component improvements")]
    public async Task<string> GetAngularComponentRecommendationsAsync(
        [Description("Path to the Angular component TypeScript file (.component.ts)")]
        string filePath)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);

            if (!filePath.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be an Angular component (.component.ts file).", nameof(filePath));
            }

            AngularComponentAnalysisResult analysisResult = await angularRefactorer.AnalyzeComponentAsync(filePath);
            
            if (!analysisResult.Success || analysisResult.Component == null)
            {
                throw new InvalidOperationException($"Failed to analyze component: {analysisResult.ErrorMessage}");
            }

            var recommendations = new
            {
                component_name = analysisResult.Component.Name,
                complexity_score = analysisResult.ComplexityScore,
                needs_refactoring = analysisResult.NeedsRefactoring,
                recommendations = analysisResult.Recommendations,
                potential_issues = analysisResult.PotentialIssues,
                component_metrics = new
                {
                    methods_count = analysisResult.Component.Methods.Count,
                    lifecycle_hooks = analysisResult.Component.LifecycleHooks.Count,
                    input_properties = analysisResult.Component.InputProperties.Count,
                    output_properties = analysisResult.Component.OutputProperties.Count,
                    injected_services = analysisResult.Component.InjectedServices.Count,
                    line_count = analysisResult.Component.LineCount
                },
                refactoring_suggestions = new
                {
                    extract_business_logic = analysisResult.Component.Methods.Count(m => m is { IsLifecycleHook: false, UsedInTemplate: false }) > 3,
                    optimize_change_detection = !analysisResult.Component.Name.Contains("OnPush"),
                    separate_lifecycle_logic = analysisResult.Component.LifecycleHooks.Count > 3,
                    reduce_dependencies = analysisResult.Component.InjectedServices.Count > 5
                }
            };

            return recommendations;
        });
    }

    [McpServerTool]
    [Description("Analyze Angular component architecture and identify improvement opportunities")]
    public async Task<string> AnalyzeAngularArchitectureAsync(
        [Description("Path to the Angular component TypeScript file (.component.ts)")]
        string filePath)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateFilePath(filePath);

            if (!filePath.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be an Angular component (.component.ts file).", nameof(filePath));
            }

            AngularComponentAnalysisResult analysisResult = await angularRefactorer.AnalyzeComponentAsync(filePath);
            
            if (!analysisResult.Success || analysisResult.Component == null)
            {
                throw new InvalidOperationException($"Failed to analyze component: {analysisResult.ErrorMessage}");
            }

            AngularComponent component = analysisResult.Component;
            
            var architectureAnalysis = new
            {
                component_info = new
                {
                    name = component.Name,
                    selector = component.Selector,
                    is_standalone = component.Standalone,
                    template_url = component.TemplateUrl,
                    style_urls_count = component.StyleUrls.Count,
                    is_exported = component.IsExported
                },
                structure_analysis = new
                {
                    total_methods = component.Methods.Count,
                    lifecycle_hooks = component.LifecycleHooks.Select(h => new { 
                        name = h.Name, 
                        @interface = h.Interface, 
                        is_async = h.IsAsync 
                    }),
                    business_logic_methods = component.Methods.Where(m => m is { IsLifecycleHook: false, UsedInTemplate: false }).Select(m => new {
                        name = m.Name,
                        is_async = m.IsAsync,
                        parameters_count = m.Parameters.Count,
                        access_modifier = m.AccessModifier
                    }),
                    template_methods = component.Methods.Where(m => m.UsedInTemplate).Select(m => m.Name)
                },
                dependency_analysis = new
                {
                    injected_services = component.InjectedServices.Select(s => new {
                        service_name = s.ServiceName,
                        property_name = s.PropertyName,
                        is_private = s.IsPrivate,
                        import_path = s.ImportPath
                    }),
                    input_properties = component.InputProperties.Select(p => new {
                        name = p.Name,
                        type = p.Type,
                        is_optional = p.IsOptional
                    }),
                    output_properties = component.OutputProperties.Select(p => new {
                        name = p.Name,
                        type = p.Type
                    })
                },
                complexity_assessment = new
                {
                    complexity_score = analysisResult.ComplexityScore,
                    complexity_level = analysisResult.ComplexityScore switch
                    {
                        < 30 => "Low",
                        < 60 => "Medium", 
                        < 80 => "High",
                        _ => "Very High"
                    },
                    needs_refactoring = analysisResult.NeedsRefactoring,
                    primary_concerns = analysisResult.Recommendations.Take(3)
                },
                angular_best_practices = new
                {
                    uses_onpush_change_detection = component.Name.Contains("OnPush") || analysisResult.Component.FilePath.Contains("OnPush"),
                    has_proper_lifecycle_management = component.LifecycleHooks.Any(h => h.Name == "ngOnDestroy"),
                    separates_concerns = component.Methods.Count(m => m.IsLifecycleHook) < component.Methods.Count * 0.5,
                    dependency_injection_score = component.InjectedServices.Count <= 8 ? "Good" : "Concerning"
                }
            };

            return architectureAnalysis;
        });
    }

    #endregion
}
