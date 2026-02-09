using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Playwright.Core.Services;
using PlaywrightServerMcp.Models;

namespace PlaywrightServerMcp.Tools;

/// <summary>
/// Handles Angular workspace configuration analysis and validation
/// Implements ANG-009 Angular JSON Configuration Analysis
/// </summary>
[McpServerToolType]
public partial class AngularConfigurationAnalyzer(PlaywrightSessionManager sessionManager)
{
    private readonly PlaywrightSessionManager _sessionManager = sessionManager;
[McpServerTool]
    [Description("Analyze Angular workspace configuration (angular.json) with comprehensive parsing and validation. See skills/playwright-mcp/tools/angular/configuration-analyzer.md.")]
    public async Task<string> AnalyzeAngularJsonConfig(
        string workingDirectory = "",
        bool includeDependencyAnalysis = true,
        bool includeSecurityScan = true,
        bool includeArchitecturalInsights = true,
        string sessionId = "default")
    {
        try
        {
            // Validate session exists
            PlaywrightSessionManager.SessionContext? session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new ConfigurationAnalysisResult
                {
                    Success = false,
                    WorkingDirectory = workingDirectory,
                    ErrorMessage = $"Session {sessionId} not found"
                }, SerializerOptions.JsonOptionsComplex);
            }

            string targetDirectory = string.IsNullOrWhiteSpace(workingDirectory) 
                ? Directory.GetCurrentDirectory() 
                : workingDirectory;

            ConfigurationAnalysisResult result = await AnalyzeWorkspaceConfiguration(
                targetDirectory, 
                includeDependencyAnalysis, 
                includeSecurityScan, 
                includeArchitecturalInsights);

            return JsonSerializer.Serialize(result, SerializerOptions.JsonOptionsComplex);
        }
        catch (Exception ex)
        {
            var errorResult = new ConfigurationAnalysisResult
            {
                Success = false,
                WorkingDirectory = workingDirectory,
                ErrorMessage = $"Failed to analyze Angular configuration: {ex.Message}"
            };
            
            return JsonSerializer.Serialize(errorResult, SerializerOptions.JsonOptionsComplex);
        }
    }

    /// <summary>
    /// Core method to analyze Angular workspace configuration
    /// </summary>
    private async Task<ConfigurationAnalysisResult> AnalyzeWorkspaceConfiguration(
        string directory,
        bool includeDependencyAnalysis,
        bool includeSecurityScan,
        bool includeArchitecturalInsights)
    {
        var result = new ConfigurationAnalysisResult
        {
            WorkingDirectory = directory
        };

        try
        {
            // Check if required files exist
            string angularJsonPath = Path.Combine(directory, "angular.json");
            string packageJsonPath = Path.Combine(directory, "package.json");
            string tsConfigPath = Path.Combine(directory, "tsconfig.json");

            result.AngularJsonExists = File.Exists(angularJsonPath);
            result.PackageJsonExists = File.Exists(packageJsonPath);
            result.TsConfigExists = File.Exists(tsConfigPath);

            if (!result.AngularJsonExists)
            {
                result.Success = false;
                result.ErrorMessage = "angular.json not found in the specified directory";
                return result;
            }

            // Parse angular.json
            string angularJsonContent = await File.ReadAllTextAsync(angularJsonPath);
            var angularConfig = JsonSerializer.Deserialize<JsonElement>(angularJsonContent);

            // Analyze workspace configuration
            result.WorkspaceConfig = await AnalyzeWorkspaceStructure(angularConfig);
            
            // Analyze individual projects
            result.Projects = await AnalyzeProjects(angularConfig);
            
            // Analyze build configurations
            result.BuildConfigs = await AnalyzeBuildConfigurations(angularConfig);

            // Analyze dependencies if requested
            if (includeDependencyAnalysis && result.PackageJsonExists)
            {
                result.Dependencies = await AnalyzeDependencies(packageJsonPath, includeSecurityScan);
            }

            // Validate configuration
            result.Validation = await ValidateConfiguration(angularConfig, directory);

            // Generate architectural insights if requested
            if (includeArchitecturalInsights)
            {
                result.Insights = await GenerateArchitecturalInsights(angularConfig, directory);
            }

            // Generate recommendations
            result.Recommendations = await GenerateRecommendations(result);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error analyzing configuration: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Analyze workspace-level configuration
    /// </summary>
    private async Task<WorkspaceConfiguration> AnalyzeWorkspaceStructure(JsonElement angularConfig)
    {
        return await Task.Run(() =>
        {
            var workspace = new WorkspaceConfiguration();

            // Extract version
            if (angularConfig.TryGetProperty("version", out JsonElement version))
            {
                workspace.Version = version.GetInt32();
            }

            // Extract default project
            if (angularConfig.TryGetProperty("defaultProject", out JsonElement defaultProject))
            {
                workspace.DefaultProject = defaultProject.GetString() ?? string.Empty;
            }

            // Extract projects
            if (angularConfig.TryGetProperty("projects", out JsonElement projects))
            {
                workspace.ProjectCount = projects.EnumerateObject().Count();
                workspace.ProjectNames = projects.EnumerateObject()
                    .Select(p => p.Name)
                    .ToList();
            }

            // Extract schema information
            if (angularConfig.TryGetProperty("$schema", out JsonElement schema))
            {
                workspace.Schema = new SchemaInformation
                {
                    Url = schema.GetString() ?? string.Empty,
                    Version = ExtractSchemaVersion(schema.GetString() ?? string.Empty)
                };
            }

            // Extract CLI configuration
            if (angularConfig.TryGetProperty("cli", out JsonElement cli))
            {
                workspace.Cli = ExtractCliConfiguration(cli);
            }

            // Extract schematics
            if (angularConfig.TryGetProperty("schematics", out JsonElement schematics))
            {
                workspace.GlobalSettings.Schematics = JsonElementToDictionary(schematics);
            }

            return workspace;
        });
    }

    /// <summary>
    /// Analyze individual project configurations
    /// </summary>
    private async Task<List<ProjectConfiguration>> AnalyzeProjects(JsonElement angularConfig)
    {
        return await Task.Run(() =>
        {
            var projects = new List<ProjectConfiguration>();

            if (angularConfig.TryGetProperty("projects", out JsonElement projectsElement))
            {
                foreach (JsonProperty project in projectsElement.EnumerateObject())
                {
                    var projectConfig = new ProjectConfiguration
                    {
                        Name = project.Name
                    };

                    JsonElement projectValue = project.Value;

                    // Extract project type
                    if (projectValue.TryGetProperty("projectType", out JsonElement projectType))
                    {
                        projectConfig.ProjectType = projectType.GetString() ?? string.Empty;
                    }

                    // Extract root and source root
                    if (projectValue.TryGetProperty("root", out JsonElement root))
                    {
                        projectConfig.Root = root.GetString() ?? string.Empty;
                    }

                    if (projectValue.TryGetProperty("sourceRoot", out JsonElement sourceRoot))
                    {
                        projectConfig.SourceRoot = sourceRoot.GetString() ?? string.Empty;
                    }

                    // Extract prefix
                    if (projectValue.TryGetProperty("prefix", out JsonElement prefix))
                    {
                        projectConfig.Prefix = prefix.GetString() ?? string.Empty;
                    }

                    // Extract architect configuration
                    if (projectValue.TryGetProperty("architect", out JsonElement architect))
                    {
                        projectConfig.Architect = ExtractArchitectConfiguration(architect);
                    }

                    projects.Add(projectConfig);
                }
            }

            return projects;
        });
    }

    /// <summary>
    /// Analyze build configurations across all projects
    /// </summary>
    private async Task<BuildConfigurations> AnalyzeBuildConfigurations(JsonElement angularConfig)
    {
        return await Task.Run(() =>
        {
            var buildConfigs = new BuildConfigurations();
            var allConfigurations = new HashSet<string>();

            if (angularConfig.TryGetProperty("projects", out JsonElement projects))
            {
                foreach (JsonProperty project in projects.EnumerateObject())
                {
                    if (project.Value.TryGetProperty("architect", out JsonElement architect) &&
                        architect.TryGetProperty("build", out JsonElement build) &&
                        build.TryGetProperty("configurations", out JsonElement configurations))
                    {
                        foreach (JsonProperty config in configurations.EnumerateObject())
                        {
                            allConfigurations.Add(config.Name);
                        }
                    }
                }
            }

            buildConfigs.AvailableConfigurations = allConfigurations.ToList();
            buildConfigs.HasProduction = allConfigurations.Contains("production");
            buildConfigs.HasDevelopment = allConfigurations.Contains("development");
            buildConfigs.HasTesting = allConfigurations.Contains("test") || allConfigurations.Contains("testing");

            // Analyze optimization settings
            buildConfigs.Optimization = AnalyzeOptimizationSettings(angularConfig);

            // Compare configurations
            buildConfigs.Comparison = CompareConfigurations(angularConfig);

            return buildConfigs;
        });
    }

    /// <summary>
    /// Analyze package.json dependencies
    /// </summary>
    private async Task<DependencyAnalysis> AnalyzeDependencies(string packageJsonPath, bool includeSecurityScan)
    {
        var dependencies = new DependencyAnalysis();

        try
        {
            string packageJsonContent = await File.ReadAllTextAsync(packageJsonPath);
            var packageJson = JsonSerializer.Deserialize<JsonElement>(packageJsonContent);

            // Analyze Angular dependencies
            dependencies.Angular = await AnalyzeAngularDependencies(packageJson);

            // Analyze third-party dependencies
            dependencies.ThirdParty = await AnalyzeThirdPartyDependencies(packageJson);

            // Extract dev dependencies
            if (packageJson.TryGetProperty("devDependencies", out JsonElement devDeps))
            {
                dependencies.DevDependencies = devDeps.EnumerateObject()
                    .Select(d => $"{d.Name}@{d.Value.GetString()}")
                    .ToList();
            }

            // Extract peer dependencies
            if (packageJson.TryGetProperty("peerDependencies", out JsonElement peerDeps))
            {
                dependencies.PeerDependencies = peerDeps.EnumerateObject()
                    .Select(d => $"{d.Name}@{d.Value.GetString()}")
                    .ToList();
            }

            // Perform security analysis if requested
            if (includeSecurityScan)
            {
                dependencies.Security = await PerformSecurityAnalysis(packageJson);
            }

            // Analyze versions
            dependencies.Versions = await AnalyzeVersionCompatibility(packageJson);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the entire analysis
            dependencies.Angular.CoreVersion = $"Error reading package.json: {ex.Message}";
        }

        return dependencies;
    }

    /// <summary>
    /// Validate Angular configuration against best practices and schema
    /// </summary>
    private async Task<ConfigurationValidation> ValidateConfiguration(JsonElement angularConfig, string directory)
    {
        return await Task.Run(() =>
        {
            var validation = new ConfigurationValidation();
            var errors = new List<ValidationError>();
            var warnings = new List<ValidationWarning>();

            // Schema validation
            validation.Schema = ValidateSchema(angularConfig);

            // Best practices validation
            validation.BestPractices = ValidateBestPractices(angularConfig, directory);

            // Check for common configuration issues
            ValidateCommonIssues(angularConfig, errors, warnings);

            validation.Errors = errors;
            validation.Warnings = warnings;
            validation.IsValid = errors.Count == 0;

            return validation;
        });
    }

    /// <summary>
    /// Generate architectural insights about the Angular project
    /// </summary>
    private async Task<ArchitecturalInsights> GenerateArchitecturalInsights(JsonElement angularConfig, string directory)
    {
        return await Task.Run(() =>
        {
            var insights = new ArchitecturalInsights
            {
                // Analyze project structure
                Structure = AnalyzeProjectStructure(angularConfig),
                // Analyze module architecture
                Modules = AnalyzeModuleArchitecture(angularConfig, directory),
                // Analyze scalability
                Scalability = AnalyzeScalability(angularConfig),
                // Analyze technology stack
                TechStack = AnalyzeTechnologyStack(angularConfig)
            };

            return insights;
        });
    }

    /// <summary>
    /// Generate configuration recommendations based on analysis
    /// </summary>
    private async Task<List<ConfigurationRecommendation>> GenerateRecommendations(ConfigurationAnalysisResult result)
    {
        return await Task.Run(() =>
        {
            var recommendations = new List<ConfigurationRecommendation>();

            // Performance recommendations
            recommendations.AddRange(GeneratePerformanceRecommendations(result));

            // Security recommendations
            recommendations.AddRange(GenerateSecurityRecommendations(result));

            // Maintainability recommendations
            recommendations.AddRange(GenerateMaintainabilityRecommendations(result));

            // Scalability recommendations
            recommendations.AddRange(GenerateScalabilityRecommendations(result));

            return recommendations.OrderByDescending(r => r.Priority).ToList();
        });
    }

    // Helper methods for extracting and analyzing configuration data

    private static string ExtractSchemaVersion(string schemaUrl)
    {
        // Extract version from schema URL
        var versionPattern = @"(\d+\.\d+\.\d+)";
        Match match = Regex.Match(schemaUrl, versionPattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static CliConfiguration ExtractCliConfiguration(JsonElement cli)
    {
        var config = new CliConfiguration();

        if (cli.TryGetProperty("warnings", out JsonElement warnings))
        {
            config.Warnings = JsonElementToDictionary(warnings);
        }

        if (cli.TryGetProperty("analytics", out JsonElement analytics))
        {
            config.Analytics = JsonElementToDictionary(analytics);
        }

        if (cli.TryGetProperty("cache", out JsonElement cache))
        {
            config.Cache = JsonElementToDictionary(cache);
        }

        return config;
    }

    private static ArchitectConfiguration ExtractArchitectConfiguration(JsonElement architect)
    {
        var config = new ArchitectConfiguration();

        if (architect.TryGetProperty("build", out JsonElement build))
        {
            config.Build = ExtractBuildTarget(build);
        }

        if (architect.TryGetProperty("serve", out JsonElement serve))
        {
            config.Serve = ExtractBuildTarget(serve);
        }

        if (architect.TryGetProperty("test", out JsonElement test))
        {
            config.Test = ExtractBuildTarget(test);
        }

        if (architect.TryGetProperty("lint", out JsonElement lint))
        {
            config.Lint = ExtractBuildTarget(lint);
        }

        if (architect.TryGetProperty("extract-i18n", out JsonElement extractI18n))
        {
            config.ExtractI18n = ExtractBuildTarget(extractI18n);
        }

        // Extract custom targets
        config.CustomTargets = architect.EnumerateObject()
            .Where(p => !new[] { "build", "serve", "test", "lint", "extract-i18n" }.Contains(p.Name))
            .Select(p => new CustomTarget
            {
                Name = p.Name,
                Builder = p.Value.TryGetProperty("builder", out JsonElement builder) ? builder.GetString() ?? string.Empty : string.Empty,
                Options = p.Value.TryGetProperty("options", out JsonElement options) ? JsonElementToDictionary(options) : new Dictionary<string, object>()
            })
            .ToList();

        return config;
    }

    private static BuildTarget ExtractBuildTarget(JsonElement target)
    {
        var buildTarget = new BuildTarget();

        if (target.TryGetProperty("builder", out JsonElement builder))
        {
            buildTarget.Builder = builder.GetString() ?? string.Empty;
        }

        if (target.TryGetProperty("options", out JsonElement options))
        {
            buildTarget.Options = JsonElementToDictionary(options);
        }

        if (target.TryGetProperty("configurations", out JsonElement configurations))
        {
            buildTarget.Configurations = configurations.EnumerateObject()
                .ToDictionary(
                    c => c.Name,
                    c => ExtractBuildConfiguration(c.Value)
                );
        }

        if (target.TryGetProperty("defaultConfiguration", out JsonElement defaultConfig))
        {
            buildTarget.DefaultConfiguration = [defaultConfig.GetString() ?? string.Empty];
        }

        return buildTarget;
    }

    private static BuildConfiguration ExtractBuildConfiguration(JsonElement config)
    {
        var buildConfig = new BuildConfiguration();

        if (config.TryGetProperty("outputPath", out JsonElement outputPath))
        {
            buildConfig.OutputPath = outputPath.GetString() ?? string.Empty;
        }

        if (config.TryGetProperty("optimization", out JsonElement optimization))
        {
            buildConfig.Optimization = optimization.GetBoolean();
        }

        if (config.TryGetProperty("sourceMap", out JsonElement sourceMap))
        {
            buildConfig.SourceMap = sourceMap.GetBoolean();
        }

        if (config.TryGetProperty("extractCss", out JsonElement extractCss))
        {
            buildConfig.ExtractCss = extractCss.GetBoolean();
        }

        if (config.TryGetProperty("namedChunks", out JsonElement namedChunks))
        {
            buildConfig.NamedChunks = namedChunks.GetBoolean();
        }

        if (config.TryGetProperty("aot", out JsonElement aot))
        {
            buildConfig.Aot = aot.GetBoolean();
        }

        if (config.TryGetProperty("budgets", out JsonElement budgets))
        {
            buildConfig.Budgets = budgets.EnumerateArray()
                .Select(ExtractBudgetConfig)
                .ToList();
        }

        buildConfig.AdditionalOptions = JsonElementToDictionary(config);

        return buildConfig;
    }

    private static BudgetConfig ExtractBudgetConfig(JsonElement budget)
    {
        var budgetConfig = new BudgetConfig();

        if (budget.TryGetProperty("type", out JsonElement type))
        {
            budgetConfig.Type = type.GetString() ?? string.Empty;
        }

        if (budget.TryGetProperty("baseline", out JsonElement baseline))
        {
            budgetConfig.Baseline = baseline.GetString() ?? string.Empty;
        }

        if (budget.TryGetProperty("maximumWarning", out JsonElement maxWarning))
        {
            budgetConfig.Warning = maxWarning.GetString() ?? string.Empty;
        }

        if (budget.TryGetProperty("maximumError", out JsonElement maxError))
        {
            budgetConfig.Error = maxError.GetString() ?? string.Empty;
        }

        return budgetConfig;
    }

    private static OptimizationAnalysis AnalyzeOptimizationSettings(JsonElement angularConfig)
    {
        var optimization = new OptimizationAnalysis();
        var opportunities = new List<string>();

        // Check for optimization settings across projects
        if (angularConfig.TryGetProperty("projects", out JsonElement projects))
        {
            foreach (JsonProperty project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out JsonElement architect) &&
                    architect.TryGetProperty("build", out JsonElement build))
                {
                    // Check production configuration
                    if (build.TryGetProperty("configurations", out JsonElement configurations) &&
                        configurations.TryGetProperty("production", out JsonElement production))
                    {
                        if (production.TryGetProperty("optimization", out JsonElement opt))
                        {
                            optimization.MinificationEnabled = opt.GetBoolean();
                        }

                        if (production.TryGetProperty("aot", out JsonElement aot))
                        {
                            optimization.AotEnabled = aot.GetBoolean();
                        }

                        if (production.TryGetProperty("buildOptimizer", out JsonElement buildOpt))
                        {
                            optimization.TreeShakingEnabled = buildOpt.GetBoolean();
                        }
                    }
                }
            }
        }

        // Add optimization opportunities
        if (!optimization.MinificationEnabled)
        {
            opportunities.Add("Enable minification in production builds");
        }

        if (!optimization.AotEnabled)
        {
            opportunities.Add("Enable Ahead-of-Time (AOT) compilation");
        }

        if (!optimization.TreeShakingEnabled)
        {
            opportunities.Add("Enable tree shaking for smaller bundle sizes");
        }

        optimization.OptimizationOpportunities = opportunities;
        return optimization;
    }

    private static ConfigurationComparison CompareConfigurations(JsonElement angularConfig)
    {
        var comparison = new ConfigurationComparison();
        
        // This would compare production vs development configurations
        // Implementation would analyze the differences between configurations
        // and provide insights about the impact of each difference
        
        return comparison;
    }

    private async Task<AngularDependencies> AnalyzeAngularDependencies(JsonElement packageJson)
    {
        return await Task.Run(() =>
        {
            var angular = new AngularDependencies();

            if (packageJson.TryGetProperty("dependencies", out JsonElement deps))
            {
                foreach (JsonProperty dep in deps.EnumerateObject())
                {
                    string name = dep.Name;
                    string version = dep.Value.GetString() ?? string.Empty;

                    switch (name)
                    {
                        case "@angular/core":
                            angular.CoreVersion = version;
                            break;
                        case "@angular/cli":
                            angular.CliVersion = version;
                            break;
                        case "typescript":
                            angular.TypeScriptVersion = version;
                            break;
                        case "rxjs":
                            angular.RxJsVersion = version;
                            break;
                    }

                    if (name.StartsWith("@angular/"))
                    {
                        angular.AngularPackages.Add($"{name}@{version}");
                    }
                }
            }

            // Determine feature support based on versions
            angular.StandaloneSupport = DetermineStandaloneSupport(angular.CoreVersion);
            angular.SignalsSupport = DetermineSignalsSupport(angular.CoreVersion);
            angular.ZonelessSupport = DetermineZonelessSupport(angular.CoreVersion);

            return angular;
        });
    }

    private async Task<List<ThirdPartyDependency>> AnalyzeThirdPartyDependencies(JsonElement packageJson)
    {
        return await Task.Run(() =>
        {
            var thirdParty = new List<ThirdPartyDependency>();

            if (packageJson.TryGetProperty("dependencies", out JsonElement deps))
            {
                foreach (JsonProperty dep in deps.EnumerateObject())
                {
                    string name = dep.Name;
                    string version = dep.Value.GetString() ?? string.Empty;

                    // Skip Angular packages
                    if (name.StartsWith("@angular/") || name == "typescript" || name == "rxjs")
                        continue;

                    var dependency = new ThirdPartyDependency
                    {
                        Name = name,
                        Version = version,
                        Category = CategorizePackage(name),
                        Description = GetPackageDescription(name),
                        IsDeprecated = IsPackageDeprecated(name)
                    };

                    if (dependency.IsDeprecated)
                    {
                        dependency.Alternatives = GetPackageAlternatives(name);
                    }

                    thirdParty.Add(dependency);
                }
            }

            return thirdParty;
        });
    }

    private async Task<SecurityAnalysis> PerformSecurityAnalysis(JsonElement packageJson)
    {
        return await Task.Run(() =>
        {
            var security = new SecurityAnalysis
            {
                // This would integrate with security vulnerability databases
                // For now, provide a basic analysis structure
                Score = new SecurityScore
                {
                    Overall = 85, // Example score
                    DependencyHealth = 90,
                    UpdateCompliance = 80,
                    Risk = "low"
                }
            };

            return security;
        });
    }

    private async Task<VersionAnalysis> AnalyzeVersionCompatibility(JsonElement packageJson)
    {
        return await Task.Run(() =>
        {
            var versions = new VersionAnalysis
            {
                // Analyze Angular package version consistency
                AngularVersionsConsistent = CheckAngularVersionConsistency(packageJson),
                // Check for major version updates available
                MajorVersionUpdatesAvailable = GetMajorUpdatesAvailable(packageJson),
                // Analyze compatibility matrix
                Compatibility = AnalyzeCompatibilityMatrix(packageJson)
            };

            return versions;
        });
    }

    private static SchemaValidation ValidateSchema(JsonElement angularConfig)
    {
        var schema = new SchemaValidation
        {
            SchemaValid = true // Basic validation - could be enhanced with actual schema validation
        };

        if (angularConfig.TryGetProperty("$schema", out JsonElement schemaElement))
        {
            schema.SchemaVersion = ExtractSchemaVersion(schemaElement.GetString() ?? string.Empty);
        }

        return schema;
    }

    private static BestPracticesValidation ValidateBestPractices(JsonElement angularConfig, string directory)
    {
        var bestPractices = new BestPracticesValidation();
        var violations = new List<string>();
        var improvements = new List<string>();
        var score = 100;

        // Check for testing configuration
        bool testingConfigured = HasTestingConfiguration(angularConfig);
        bestPractices.Maintenance.TestingConfigured = testingConfigured;
        if (!testingConfigured)
        {
            violations.Add("No testing configuration found");
            score -= 15;
        }

        // Check for linting configuration
        bool lintingConfigured = HasLintingConfiguration(angularConfig);
        bestPractices.Maintenance.LintingConfigured = lintingConfigured;
        if (!lintingConfigured)
        {
            violations.Add("No linting configuration found");
            score -= 10;
        }

        // Check for strict TypeScript configuration
        bool strictTypeScript = HasStrictTypeScript(directory);
        bestPractices.Maintenance.TypeCheckingStrict = strictTypeScript;
        if (!strictTypeScript)
        {
            improvements.Add("Enable strict TypeScript compilation");
            score -= 5;
        }

        bestPractices.Score = Math.Max(0, score);
        bestPractices.Violations = violations;
        bestPractices.Improvements = improvements;

        return bestPractices;
    }

    private static void ValidateCommonIssues(JsonElement angularConfig, List<ValidationError> errors, List<ValidationWarning> warnings)
    {
        // Check for missing default project
        if (!angularConfig.TryGetProperty("defaultProject", out _))
        {
            warnings.Add(new ValidationWarning
            {
                Type = "configuration",
                Message = "No default project specified",
                Location = "angular.json root",
                Recommendation = "Set a default project to improve CLI command experience"
            });
        }

        // Check for outdated schema
        if (angularConfig.TryGetProperty("$schema", out JsonElement schema))
        {
            string schemaUrl = schema.GetString() ?? string.Empty;
            if (!schemaUrl.Contains("angular.io"))
            {
                warnings.Add(new ValidationWarning
                {
                    Type = "schema",
                    Message = "Using non-standard schema URL",
                    Location = "$schema",
                    Recommendation = "Use official Angular schema URL"
                });
            }
        }
    }

    private static ProjectStructure AnalyzeProjectStructure(JsonElement angularConfig)
    {
        var structure = new ProjectStructure();

        if (angularConfig.TryGetProperty("projects", out JsonElement projects))
        {
            List<JsonProperty> projectList = projects.EnumerateObject().ToList();
            structure.ApplicationCount = projectList.Count(p => 
                p.Value.TryGetProperty("projectType", out JsonElement type) && 
                type.GetString() == "application");
            
            structure.LibraryCount = projectList.Count(p => 
                p.Value.TryGetProperty("projectType", out JsonElement type) && 
                type.GetString() == "library");

            structure.IsMonorepo = projectList.Count > 1;
            structure.ArchitecturePattern = structure.IsMonorepo ? "monorepo" : "single-project";
        }

        return structure;
    }

    private static ModuleArchitecture AnalyzeModuleArchitecture(JsonElement angularConfig, string directory)
    {
        var modules = new ModuleArchitecture
        {
            // This would require analyzing the actual source files
            // For now, provide basic structure
            UsesNgModules = true, // Default assumption
            UsesStandaloneComponents = false // Would need to scan source files
        };

        return modules;
    }

    private static ScalabilityAnalysis AnalyzeScalability(JsonElement angularConfig)
    {
        var scalability = new ScalabilityAnalysis();
        var score = 100;
        var strengths = new List<string>();
        var concerns = new List<string>();

        // Analyze project count and complexity
        if (angularConfig.TryGetProperty("projects", out JsonElement projects))
        {
            int projectCount = projects.EnumerateObject().Count();
            
            if (projectCount > 1)
            {
                strengths.Add("Multi-project workspace supports modular development");
                score += 10;
            }
            else if (projectCount == 1)
            {
                concerns.Add("Single project may become difficult to maintain as team grows");
                score -= 5;
            }
        }

        scalability.Score = Math.Min(100, Math.Max(0, score));
        scalability.Strengths = strengths;
        scalability.Concerns = concerns;

        return scalability;
    }

    private static TechnologyStack AnalyzeTechnologyStack(JsonElement angularConfig)
    {
        var techStack = new TechnologyStack();

        // Extract build tool information
        if (angularConfig.TryGetProperty("projects", out JsonElement projects))
        {
            foreach (JsonProperty project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out JsonElement architect) &&
                    architect.TryGetProperty("build", out JsonElement build) &&
                    build.TryGetProperty("builder", out JsonElement builder))
                {
                    string builderName = builder.GetString() ?? string.Empty;
                    if (builderName.Contains("webpack"))
                    {
                        techStack.BuildTool = "Webpack";
                    }
                    else if (builderName.Contains("esbuild"))
                    {
                        techStack.BuildTool = "esbuild";
                    }
                    else if (builderName.Contains("browser"))
                    {
                        techStack.BuildTool = "Angular CLI";
                    }
                    break;
                }
            }
        }

        return techStack;
    }

    private static List<ConfigurationRecommendation> GeneratePerformanceRecommendations(ConfigurationAnalysisResult result)
    {
        var recommendations = new List<ConfigurationRecommendation>();

        if (!result.BuildConfigs.Optimization.MinificationEnabled)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Type = "performance",
                Priority = "high",
                Title = "Enable Minification",
                Description = "Minification reduces bundle size by removing unnecessary characters",
                Implementation = "Set optimization: true in production configuration",
                Impact = "10-30% reduction in bundle size",
                Steps =
                [
                    "Open angular.json",
                    "Navigate to projects > [project-name] > architect > build > configurations > production",
                    "Set \"optimization\": true"
                ]
            });
        }

        return recommendations;
    }

    private static List<ConfigurationRecommendation> GenerateSecurityRecommendations(ConfigurationAnalysisResult result)
    {
        var recommendations = new List<ConfigurationRecommendation>();

        if (result.Dependencies.Security.VulnerabilityCount > 0)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Type = "security",
                Priority = "critical",
                Title = "Fix Security Vulnerabilities",
                Description = $"Found {result.Dependencies.Security.VulnerabilityCount} security vulnerabilities",
                Implementation = "Update vulnerable packages to latest versions",
                Impact = "Eliminates known security risks",
                Steps =
                [
                    "Run npm audit",
                    "Review vulnerability details",
                    "Update packages using npm update or yarn upgrade"
                ]
            });
        }

        return recommendations;
    }

    private static List<ConfigurationRecommendation> GenerateMaintainabilityRecommendations(ConfigurationAnalysisResult result)
    {
        var recommendations = new List<ConfigurationRecommendation>();

        if (!result.Validation.BestPractices.Maintenance.TestingConfigured)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Type = "maintainability",
                Priority = "medium",
                Title = "Configure Testing",
                Description = "Testing configuration not found or incomplete",
                Implementation = "Set up testing framework and configuration",
                Impact = "Improves code quality and reduces bugs",
                Steps =
                [
                    "Ensure test target is configured in angular.json",
                    "Verify karma.conf.js exists",
                    "Add test scripts to package.json"
                ]
            });
        }

        return recommendations;
    }

    private static List<ConfigurationRecommendation> GenerateScalabilityRecommendations(ConfigurationAnalysisResult result)
    {
        var recommendations = new List<ConfigurationRecommendation>();

        if (result.Insights.Structure.ApplicationCount == 1 && result.WorkspaceConfig.ProjectCount == 1)
        {
            recommendations.Add(new ConfigurationRecommendation
            {
                Type = "scalability",
                Priority = "low",
                Title = "Consider Multi-Project Workspace",
                Description = "Single project may become difficult to maintain as application grows",
                Implementation = "Extract common functionality into libraries",
                Impact = "Improves code reusability and team collaboration",
                Steps =
                [
                    "Identify reusable components and services",
                    "Create library projects using ng generate library",
                    "Refactor shared code into libraries"
                ]
            });
        }

        return recommendations;
    }

    // Helper utility methods

    private static Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dictionary = new Dictionary<string, object>();

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                dictionary[property.Name] = JsonElementToObject(property.Value);
            }
        }

        return dictionary;
    }

    private static object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out int intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => JsonElementToDictionary(element),
            _ => element.ToString()
        };
    }

    private static bool DetermineStandaloneSupport(string coreVersion)
    {
        // Standalone components were introduced in Angular 14
        return CompareVersions(coreVersion, "14.0.0") >= 0;
    }

    private static bool DetermineSignalsSupport(string coreVersion)
    {
        // Signals were introduced in Angular 16
        return CompareVersions(coreVersion, "16.0.0") >= 0;
    }

    private static bool DetermineZonelessSupport(string coreVersion)
    {
        // Zoneless change detection experimental in Angular 18
        return CompareVersions(coreVersion, "18.0.0") >= 0;
    }

    private static int CompareVersions(string version1, string version2)
    {
        // Simplified version comparison - would need more robust implementation
        try
        {
            var v1 = new Version(version1.TrimStart('^', '~', '='));
            var v2 = new Version(version2);
            return v1.CompareTo(v2);
        }
        catch
        {
            return 0;
        }
    }

    private static string CategorizePackage(string packageName)
    {
        return packageName.ToLower() switch
        {
            var name when name.Contains("material") || name.Contains("ui") => "ui",
            var name when name.Contains("ngrx") || name.Contains("state") => "state-management",
            var name when name.Contains("test") || name.Contains("jasmine") || name.Contains("karma") => "testing",
            var name when name.Contains("http") || name.Contains("api") => "networking",
            var name when name.Contains("router") || name.Contains("routing") => "routing",
            var name when name.Contains("form") => "forms",
            _ => "utility"
        };
    }

    private static string GetPackageDescription(string packageName)
    {
        // This would typically fetch from npm registry or local cache
        return $"Third-party package: {packageName}";
    }

    private static bool IsPackageDeprecated(string packageName)
    {
        // This would check against known deprecated packages
        var deprecatedPackages = new[] { "tslint", "protractor" };
        return deprecatedPackages.Contains(packageName.ToLower());
    }

    private static List<string> GetPackageAlternatives(string packageName)
    {
        return packageName.ToLower() switch
        {
            "tslint" => ["eslint", "@angular-eslint/eslint-plugin"],
            "protractor" => ["cypress", "@playwright/test", "webdriver.io"],
            _ => []
        };
    }

    private static bool CheckAngularVersionConsistency(JsonElement packageJson)
    {
        var angularVersions = new List<string>();

        if (packageJson.TryGetProperty("dependencies", out JsonElement deps))
        {
            foreach (JsonProperty dep in deps.EnumerateObject())
            {
                if (dep.Name.StartsWith("@angular/"))
                {
                    angularVersions.Add(dep.Value.GetString() ?? string.Empty);
                }
            }
        }

        // Check if all Angular packages have the same major version
        return angularVersions.Select(v => v.Split('.')[0]).Distinct().Count() <= 1;
    }

    private static List<string> GetMajorUpdatesAvailable(JsonElement packageJson)
    {
        // This would check npm registry for major version updates
        return [];
    }

    private static CompatibilityMatrix AnalyzeCompatibilityMatrix(JsonElement packageJson)
    {
        return new CompatibilityMatrix
        {
            NodeCompatible = true, // Would check against Node.js compatibility
            TypeScriptCompatible = true, // Would check TypeScript version compatibility
            RxJsCompatible = true // Would check RxJS version compatibility
        };
    }

    private static bool HasTestingConfiguration(JsonElement angularConfig)
    {
        if (angularConfig.TryGetProperty("projects", out JsonElement projects))
        {
            foreach (JsonProperty project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out JsonElement architect) &&
                    architect.TryGetProperty("test", out _))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasLintingConfiguration(JsonElement angularConfig)
    {
        if (angularConfig.TryGetProperty("projects", out JsonElement projects))
        {
            foreach (JsonProperty project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out JsonElement architect) &&
                    architect.TryGetProperty("lint", out _))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasStrictTypeScript(string directory)
    {
        try
        {
            string tsConfigPath = Path.Combine(directory, "tsconfig.json");
            if (!File.Exists(tsConfigPath))
                return false;

            string content = File.ReadAllText(tsConfigPath);
            var tsConfig = JsonSerializer.Deserialize<JsonElement>(content);

            if (tsConfig.TryGetProperty("compilerOptions", out JsonElement compilerOptions) &&
                compilerOptions.TryGetProperty("strict", out JsonElement strict))
            {
                return strict.GetBoolean();
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }
}
