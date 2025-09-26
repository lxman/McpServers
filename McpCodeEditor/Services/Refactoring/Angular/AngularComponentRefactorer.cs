using McpCodeEditor.Models.Angular;
using McpCodeEditor.Services.Analysis;
using McpCodeEditor.Services.TypeScript;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services.Refactoring.Angular;

/// <summary>
/// Angular component refactoring service for specialized Angular component operations
/// Provides Angular-specific refactoring capabilities building on TypeScript infrastructure
/// </summary>
public class AngularComponentRefactorer(
    ILogger<AngularComponentRefactorer> logger,
    TypeScriptAnalysisService typeScriptAnalysis,
    TypeScriptFileResolver fileResolver,
    IBackupService backupService,
    IChangeTrackingService changeTrackingService)
{
    // Angular-specific regex patterns
    private static readonly Regex ComponentDecoratorPattern = new(
        @"@Component\s*\(\s*\{([^}]+)\}\s*\)", 
        RegexOptions.Compiled | RegexOptions.Singleline);
    
    private static readonly Regex SelectorPattern = new(
        @"selector\s*:\s*['""]([^'""]+)['""]", 
        RegexOptions.Compiled);
    
    private static readonly Regex TemplateUrlPattern = new(
        @"templateUrl\s*:\s*['""]([^'""]+)['""]", 
        RegexOptions.Compiled);
    
    private static readonly Regex StyleUrlsPattern = new(
        @"styleUrls\s*:\s*\[([^\]]+)\]", 
        RegexOptions.Compiled);
    
    private static readonly Regex InputPropertyPattern = new(
        @"@Input\s*\(\s*([^)]*)\s*\)\s+([^:]+):\s*([^=;]+)", 
        RegexOptions.Compiled);
    
    private static readonly Regex OutputPropertyPattern = new(
        @"@Output\s*\(\s*([^)]*)\s*\)\s+([^:]+):\s*([^=;]+)", 
        RegexOptions.Compiled);
    
    private static readonly Regex LifecycleHookPattern = new(
        @"ng(OnInit|OnDestroy|OnChanges|DoCheck|AfterContentInit|AfterContentChecked|AfterViewInit|AfterViewChecked)\s*\(\s*\)", 
        RegexOptions.Compiled);

    /// <summary>
    /// Analyze an Angular component file and extract comprehensive component information
    /// </summary>
    public async Task<AngularComponentAnalysisResult> AnalyzeComponentAsync(
        string filePath, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Starting Angular component analysis: {FilePath}", filePath);

            if (!File.Exists(filePath))
            {
                return new AngularComponentAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"Component file not found: {filePath}"
                };
            }

            // Read component file
            string content = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            // First, perform TypeScript analysis for base structure
            TypeScriptAnalysisResult tsAnalysisResult = await typeScriptAnalysis.AnalyzeContentAsync(content, filePath, cancellationToken);
            if (!tsAnalysisResult.Success)
            {
                return new AngularComponentAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"TypeScript analysis failed: {tsAnalysisResult.ErrorMessage}"
                };
            }

            // Create Angular component analysis
            AngularComponent component = await AnalyzeAngularSpecificFeaturesAsync(content, filePath, tsAnalysisResult, cancellationToken);
            
            // Generate recommendations and complexity analysis
            List<string> recommendations = GenerateComponentRecommendations(component);
            int complexityScore = CalculateComponentComplexity(component);

            logger.LogDebug("Angular component analysis completed: {ComponentName}, Complexity: {Complexity}", 
                component.Name, complexityScore);

            return new AngularComponentAnalysisResult
            {
                Success = true,
                Component = component,
                Recommendations = recommendations,
                ComplexityScore = complexityScore,
                NeedsRefactoring = complexityScore > 75 || recommendations.Count > 5
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing Angular component: {FilePath}", filePath);
            return new AngularComponentAnalysisResult
            {
                Success = false,
                ErrorMessage = $"Analysis failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Extract Angular lifecycle hook methods from a component
    /// </summary>
    public async Task<AngularComponentRefactoringResult> ExtractLifecycleLogicAsync(
        string filePath,
        string hookName,
        AngularRefactoringOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Extracting lifecycle logic: {HookName} from {FilePath}", hookName, filePath);

            // Create backup
            string backupId = await backupService.CreateBackupAsync(
                Path.GetDirectoryName(filePath) ?? ".",
                $"Before Angular lifecycle extraction: {hookName}");

            string content = await File.ReadAllTextAsync(filePath, cancellationToken);
            AngularComponentAnalysisResult analysisResult = await AnalyzeComponentAsync(filePath, cancellationToken);

            if (!analysisResult.Success || analysisResult.Component == null)
            {
                return new AngularComponentRefactoringResult
                {
                    Success = false,
                    ErrorMessage = "Failed to analyze component for lifecycle extraction"
                };
            }

            // Find the specific lifecycle hook
            AngularLifecycleHook? lifecycleHook = analysisResult.Component.LifecycleHooks
                .FirstOrDefault(h => h.Name.Equals(hookName, StringComparison.OrdinalIgnoreCase));

            if (lifecycleHook == null)
            {
                return new AngularComponentRefactoringResult
                {
                    Success = false,
                    ErrorMessage = $"Lifecycle hook {hookName} not found in component"
                };
            }

            // Extract and refactor lifecycle logic
            string modifiedContent = await ExtractLifecycleMethodLogicAsync(content, lifecycleHook, options);

            // Write changes
            await File.WriteAllTextAsync(filePath, modifiedContent, cancellationToken);
            
            // Track changes
            await changeTrackingService.TrackChangeAsync(
                filePath,
                content,
                modifiedContent,
                $"Angular lifecycle extraction: {hookName}",
                backupId,
                new Dictionary<string, object>
                {
                    { "lifecycle_hook", hookName },
                    { "component_name", analysisResult.Component.Name }
                });

            return new AngularComponentRefactoringResult
            {
                Success = true,
                ModifiedFiles = [filePath],
                BackupId = backupId,
                Changes = [$"Extracted {hookName} lifecycle logic"],
                Summary = $"Successfully refactored {hookName} lifecycle hook in {analysisResult.Component.Name}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting lifecycle logic: {HookName}", hookName);
            return new AngularComponentRefactoringResult
            {
                Success = false,
                ErrorMessage = $"Lifecycle extraction failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Separate business logic from Angular component presentation logic
    /// </summary>
    public async Task<AngularComponentRefactoringResult> SeparateBusinessLogicAsync(
        string filePath,
        AngularRefactoringOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Separating business logic from component: {FilePath}", filePath);

            // Create backup
            string backupId = await backupService.CreateBackupAsync(
                Path.GetDirectoryName(filePath) ?? ".",
                "Before Angular business logic separation");

            string content = await File.ReadAllTextAsync(filePath, cancellationToken);
            AngularComponentAnalysisResult analysisResult = await AnalyzeComponentAsync(filePath, cancellationToken);

            if (!analysisResult.Success || analysisResult.Component == null)
            {
                return new AngularComponentRefactoringResult
                {
                    Success = false,
                    ErrorMessage = "Failed to analyze component for business logic separation"
                };
            }

            // Identify business logic methods (not lifecycle hooks, not template event handlers)
            List<AngularMethod> businessLogicMethods = analysisResult.Component.Methods
                .Where(m => m is { IsLifecycleHook: false, UsedInTemplate: false } && !IsEventHandler(m.Name))
                .ToList();

            if (businessLogicMethods.Count == 0)
            {
                return new AngularComponentRefactoringResult
                {
                    Success = true,
                    Summary = "No business logic methods found to separate"
                };
            }

            // Create the service file for business logic
            var serviceFileName = $"{analysisResult.Component.Name.Replace("Component", "")}Service.ts";
            string servicePath = Path.Combine(Path.GetDirectoryName(filePath)!, serviceFileName);
            
            AngularComponentRefactoringResult separationResult = await CreateBusinessLogicServiceAsync(
                analysisResult.Component, 
                businessLogicMethods, 
                servicePath, 
                cancellationToken);

            if (!separationResult.Success)
            {
                return separationResult;
            }

            // Update component to use the new service
            string modifiedComponentContent = UpdateComponentToUseService(
                content, 
                analysisResult.Component, 
                businessLogicMethods, 
                serviceFileName);

            // Write changes
            await File.WriteAllTextAsync(filePath, modifiedComponentContent, cancellationToken);
            
            // Track changes
            await changeTrackingService.TrackChangeAsync(
                filePath,
                content,
                modifiedComponentContent,
                "Angular business logic separation",
                backupId,
                new Dictionary<string, object>
                {
                    { "component_name", analysisResult.Component.Name },
                    { "service_created", serviceFileName },
                    { "methods_extracted", businessLogicMethods.Count }
                });

            return new AngularComponentRefactoringResult
            {
                Success = true,
                ModifiedFiles = [filePath],
                CreatedFiles = [servicePath],
                BackupId = backupId,
                Changes = [
                    $"Created {serviceFileName} with {businessLogicMethods.Count} business logic methods",
                    $"Updated {analysisResult.Component.Name} to use the new service"
                ],
                Summary = $"Successfully separated business logic from {analysisResult.Component.Name} into {serviceFileName}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error separating business logic from component: {FilePath}", filePath);
            return new AngularComponentRefactoringResult
            {
                Success = false,
                ErrorMessage = $"Business logic separation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Optimize Angular component for better change detection performance
    /// </summary>
    public async Task<AngularComponentRefactoringResult> OptimizeChangeDetectionAsync(
        string filePath,
        AngularRefactoringOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Optimizing change detection for component: {FilePath}", filePath);

            string backupId = await backupService.CreateBackupAsync(
                Path.GetDirectoryName(filePath) ?? ".",
                "Before Angular change detection optimization");

            string content = await File.ReadAllTextAsync(filePath, cancellationToken);
            AngularComponentAnalysisResult analysisResult = await AnalyzeComponentAsync(filePath, cancellationToken);

            if (!analysisResult.Success || analysisResult.Component == null)
            {
                return new AngularComponentRefactoringResult
                {
                    Success = false,
                    ErrorMessage = "Failed to analyze component for change detection optimization"
                };
            }

            var optimizations = new List<string>();
            string modifiedContent = content;

            // Add OnPush change detection strategy if not present
            if (!content.Contains("ChangeDetectionStrategy.OnPush"))
            {
                modifiedContent = AddOnPushChangeDetection(modifiedContent);
                optimizations.Add("Added OnPush change detection strategy");
            }

            // Optimize trackBy functions for ngFor loops (would need template analysis)
            // Add immutable data patterns
            // Add ChangeDetectorRef injection if needed

            await File.WriteAllTextAsync(filePath, modifiedContent, cancellationToken);
            
            await changeTrackingService.TrackChangeAsync(
                filePath,
                content,
                modifiedContent,
                "Angular change detection optimization",
                backupId,
                new Dictionary<string, object>
                {
                    { "component_name", analysisResult.Component.Name },
                    { "optimizations_applied", optimizations.Count }
                });

            return new AngularComponentRefactoringResult
            {
                Success = true,
                ModifiedFiles = [filePath],
                BackupId = backupId,
                Changes = optimizations,
                Summary = $"Applied {optimizations.Count} change detection optimizations to {analysisResult.Component.Name}"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error optimizing change detection: {FilePath}", filePath);
            return new AngularComponentRefactoringResult
            {
                Success = false,
                ErrorMessage = $"Change detection optimization failed: {ex.Message}"
            };
        }
    }

    #region Private Methods

    /// <summary>
    /// Analyze Angular-specific features of a component
    /// </summary>
    private async Task<AngularComponent> AnalyzeAngularSpecificFeaturesAsync(
        string content, 
        string filePath, 
        TypeScriptAnalysisResult tsResult,
        CancellationToken cancellationToken)
    {
        var component = new AngularComponent
        {
            FilePath = filePath,
            LastModified = File.GetLastWriteTime(filePath),
            LineCount = content.Split('\n').Length
        };

        // Extract component metadata from @Component decorator
        ExtractComponentDecorator(content, component);
        
        // Extract component class information
        ExtractComponentClass(tsResult, component);
        
        // Extract Angular-specific properties
        ExtractAngularProperties(content, component);
        
        // Extract lifecycle hooks
        ExtractLifecycleHooks(content, tsResult, component);
        
        // Extract dependency injection
        ExtractDependencyInjection(content, component);
        
        // Analyze template usage (basic analysis)
        await AnalyzeTemplateUsageAsync(component, cancellationToken);

        return component;
    }

    /// <summary>
    /// Extract @Component decorator information
    /// </summary>
    private static void ExtractComponentDecorator(string content, AngularComponent component)
    {
        Match decoratorMatch = ComponentDecoratorPattern.Match(content);
        if (!decoratorMatch.Success) return;

        string decoratorContent = decoratorMatch.Groups[1].Value;

        // Extract selector
        Match selectorMatch = SelectorPattern.Match(decoratorContent);
        if (selectorMatch.Success)
        {
            component.Selector = selectorMatch.Groups[1].Value;
        }

        // Extract templateUrl
        Match templateUrlMatch = TemplateUrlPattern.Match(decoratorContent);
        if (templateUrlMatch.Success)
        {
            component.TemplateUrl = templateUrlMatch.Groups[1].Value;
        }

        // Extract styleUrls
        Match styleUrlsMatch = StyleUrlsPattern.Match(decoratorContent);
        if (styleUrlsMatch.Success)
        {
            string styleUrlsContent = styleUrlsMatch.Groups[1].Value;
            component.StyleUrls = styleUrlsContent
                .Split(',')
                .Select(url => url.Trim().Trim('\'', '"'))
                .Where(url => !string.IsNullOrEmpty(url))
                .ToList();
        }

        // Check for standalone
        component.Standalone = decoratorContent.Contains("standalone: true");
    }

    /// <summary>
    /// Extract component class information from TypeScript analysis
    /// </summary>
    private static void ExtractComponentClass(TypeScriptAnalysisResult tsResult, AngularComponent component)
    {
        // Find the component class (typically the exported class)
        TypeScriptClass? componentClass = tsResult.Classes.FirstOrDefault(c => c.IsExported);
        if (componentClass != null)
        {
            component.Name = componentClass.Name;
            component.StartLine = componentClass.StartLine;
            component.EndLine = componentClass.EndLine;
            component.IsExported = componentClass.IsExported;
            component.ExtendsClass = componentClass.ExtendsClass;
            component.ImplementsInterfaces = componentClass.ImplementsInterfaces;
        }

        // Extract methods from TypeScript analysis
        foreach (TypeScriptFunction tsFunction in tsResult.Functions)
        {
            component.Methods.Add(new AngularMethod
            {
                Name = tsFunction.Name,
                ReturnType = tsFunction.ReturnType,
                Parameters = tsFunction.Parameters,
                IsAsync = tsFunction.IsAsync,
                StartLine = tsFunction.StartLine,
                EndLine = tsFunction.EndLine,
                IsLifecycleHook = IsLifecycleHookName(tsFunction.Name)
            });
        }
    }

    /// <summary>
    /// Extract Angular-specific properties (@Input, @Output, etc.)
    /// </summary>
    private static void ExtractAngularProperties(string content, AngularComponent component)
    {
        // Extract @Input properties
        MatchCollection inputMatches = InputPropertyPattern.Matches(content);
        foreach (Match match in inputMatches)
        {
            component.InputProperties.Add(new AngularProperty
            {
                Name = match.Groups[2].Value.Trim(),
                Type = match.Groups[3].Value.Trim(),
                Decorator = "@Input",
                DecoratorParams = match.Groups[1].Value
            });
        }

        // Extract @Output properties
        MatchCollection outputMatches = OutputPropertyPattern.Matches(content);
        foreach (Match match in outputMatches)
        {
            component.OutputProperties.Add(new AngularProperty
            {
                Name = match.Groups[2].Value.Trim(),
                Type = match.Groups[3].Value.Trim(),
                Decorator = "@Output",
                DecoratorParams = match.Groups[1].Value
            });
        }
    }

    /// <summary>
    /// Extract lifecycle hook implementations
    /// </summary>
    private static void ExtractLifecycleHooks(string content, TypeScriptAnalysisResult tsResult, AngularComponent component)
    {
        MatchCollection hookMatches = LifecycleHookPattern.Matches(content);
        foreach (Match match in hookMatches)
        {
            string hookName = match.Groups[1].Value;
            TypeScriptFunction? tsFunction = tsResult.Functions.FirstOrDefault(f => f.Name == $"ng{hookName}");
            
            component.LifecycleHooks.Add(new AngularLifecycleHook
            {
                Name = $"ng{hookName}",
                Interface = hookName,
                Line = tsFunction?.StartLine ?? 0,
                IsImplemented = true,
                IsAsync = tsFunction?.IsAsync ?? false
            });
        }
    }

    /// <summary>
    /// Extract dependency injection information
    /// </summary>
    private static void ExtractDependencyInjection(string content, AngularComponent component)
    {
        // Look for constructor injection pattern
        Match constructorMatch = Regex.Match(content, @"constructor\s*\(\s*([^)]+)\s*\)");
        if (constructorMatch.Success)
        {
            string constructorParams = constructorMatch.Groups[1].Value;
            MatchCollection paramMatches = Regex.Matches(constructorParams, @"(?:private|public|protected)?\s*(\w+)\s*:\s*(\w+)");
            
            foreach (Match paramMatch in paramMatches)
            {
                component.InjectedServices.Add(new AngularService
                {
                    PropertyName = paramMatch.Groups[1].Value,
                    ServiceName = paramMatch.Groups[2].Value,
                    IsPrivate = constructorParams.Contains("private")
                });
            }
        }
    }

    /// <summary>
    /// Analyze template usage patterns
    /// </summary>
    private async Task AnalyzeTemplateUsageAsync(AngularComponent component, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(component.TemplateUrl)) return;

        try
        {
            string templatePath = Path.Combine(Path.GetDirectoryName(component.FilePath)!, component.TemplateUrl);
            if (File.Exists(templatePath))
            {
                string templateContent = await File.ReadAllTextAsync(templatePath, cancellationToken);
                
                // Basic template analysis - find property and method usages
                foreach (AngularMethod method in component.Methods)
                {
                    if (templateContent.Contains($"{method.Name}("))
                    {
                        method.UsedInTemplate = true;
                        component.TemplateMethods.Add(method.Name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not analyze template for component: {ComponentName}", component.Name);
        }
    }

    /// <summary>
    /// Generate recommendations for component improvement
    /// </summary>
    private static List<string> GenerateComponentRecommendations(AngularComponent component)
    {
        var recommendations = new List<string>();

        // Check component size
        if (component.Methods.Count > 15)
        {
            recommendations.Add($"Consider splitting large component ({component.Methods.Count} methods) into smaller components");
        }

        // Check lifecycle hook usage
        if (component.LifecycleHooks.Count > 5)
        {
            recommendations.Add("Consider if all lifecycle hooks are necessary - simplify component lifecycle");
        }

        // Check for business logic in component
        int businessLogicMethods = component.Methods.Count(m => m is { IsLifecycleHook: false, UsedInTemplate: false });
        if (businessLogicMethods > 5)
        {
            recommendations.Add($"Consider extracting {businessLogicMethods} business logic methods to a service");
        }

        // Check dependency injection
        if (component.InjectedServices.Count > 8)
        {
            recommendations.Add($"Component has many dependencies ({component.InjectedServices.Count}) - consider facade pattern");
        }

        return recommendations;
    }

    /// <summary>
    /// Calculate component complexity score
    /// </summary>
    private static int CalculateComponentComplexity(AngularComponent component)
    {
        var complexity = 0;
        
        complexity += component.Methods.Count * 2; // Methods add complexity
        complexity += component.InputProperties.Count; // Inputs add complexity
        complexity += component.OutputProperties.Count * 2; // Outputs add more complexity
        complexity += component.LifecycleHooks.Count * 3; // Lifecycle hooks add significant complexity
        complexity += component.InjectedServices.Count; // Dependencies add complexity
        
        return Math.Min(complexity, 100); // Cap at 100
    }

    /// <summary>
    /// Check if method name is a lifecycle hook
    /// </summary>
    private static bool IsLifecycleHookName(string methodName)
    {
        return methodName.StartsWith("ng") && methodName.Length > 2 && 
               char.IsUpper(methodName[2]);
    }

    /// <summary>
    /// Check if method is likely an event handler
    /// </summary>
    private static bool IsEventHandler(string methodName)
    {
        return methodName.StartsWith("on") || methodName.EndsWith("Click") || 
               methodName.EndsWith("Change") || methodName.EndsWith("Submit");
    }

    /// <summary>
    /// Extract lifecycle method logic for refactoring
    /// </summary>
    private static async Task<string> ExtractLifecycleMethodLogicAsync(string content, AngularLifecycleHook hook, AngularRefactoringOptions options)
    {
        // This is a simplified implementation - in practice would need more sophisticated AST manipulation
        // For now, return the content unchanged as a placeholder
        await Task.CompletedTask;
        return content;
    }

    /// <summary>
    /// Create a service file with extracted business logic
    /// </summary>
    private static async Task<AngularComponentRefactoringResult> CreateBusinessLogicServiceAsync(
        AngularComponent component, 
        List<AngularMethod> businessLogicMethods, 
        string servicePath, 
        CancellationToken cancellationToken)
    {
        try
        {
            var serviceBuilder = new StringBuilder();
            string serviceName = Path.GetFileNameWithoutExtension(servicePath);

            // Generate service file
            serviceBuilder.AppendLine("import { Injectable } from '@angular/core';");
            serviceBuilder.AppendLine();
            serviceBuilder.AppendLine("@Injectable({");
            serviceBuilder.AppendLine("  providedIn: 'root'");
            serviceBuilder.AppendLine("})");
            serviceBuilder.AppendLine($"export class {serviceName} {{");
            serviceBuilder.AppendLine();

            // Add extracted methods (simplified - would need actual method extraction)
            foreach (AngularMethod method in businessLogicMethods)
            {
                serviceBuilder.AppendLine($"  // TODO: Extract method {method.Name} from component");
                serviceBuilder.AppendLine($"  {method.Name}() {{");
                serviceBuilder.AppendLine("    // Method implementation to be extracted");
                serviceBuilder.AppendLine("  }");
                serviceBuilder.AppendLine();
            }

            serviceBuilder.AppendLine("}");

            await File.WriteAllTextAsync(servicePath, serviceBuilder.ToString(), cancellationToken);

            return new AngularComponentRefactoringResult
            {
                Success = true,
                CreatedFiles = [servicePath]
            };
        }
        catch (Exception ex)
        {
            return new AngularComponentRefactoringResult
            {
                Success = false,
                ErrorMessage = $"Failed to create service: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Update component to use the new service
    /// </summary>
    private static string UpdateComponentToUseService(string content, AngularComponent component, List<AngularMethod> extractedMethods, string serviceFileName)
    {
        // Simplified implementation - would need proper AST manipulation
        // For now, just add a comment about the service injection
        string modifiedContent = content;
        
        // Add service import (simplified)
        string serviceName = Path.GetFileNameWithoutExtension(serviceFileName);
        var importStatement = $"import {{ {serviceName} }} from './{serviceFileName.Replace(".ts", "")}';";
        
        // Insert import at the top (simplified approach)
        if (!modifiedContent.Contains(importStatement))
        {
            modifiedContent = importStatement + "\n" + modifiedContent;
        }

        return modifiedContent;
    }

    /// <summary>
    /// Add OnPush change detection strategy to component
    /// </summary>
    private static string AddOnPushChangeDetection(string content)
    {
        // Add ChangeDetectionStrategy import if not present
        if (!content.Contains("ChangeDetectionStrategy"))
        {
            content = "import { ChangeDetectionStrategy } from '@angular/core';\n" + content;
        }

        // Add changeDetection property to @Component decorator
        if (content.Contains("@Component({") && !content.Contains("changeDetection:"))
        {
            content = content.Replace("@Component({", "@Component({\n  changeDetection: ChangeDetectionStrategy.OnPush,");
        }

        return content;
    }

    #endregion
}
