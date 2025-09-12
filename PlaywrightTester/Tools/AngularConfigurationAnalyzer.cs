using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// Handles Angular workspace configuration analysis and validation
/// Implements ANG-009 Angular JSON Configuration Analysis
/// </summary>
[McpServerToolType]
public class AngularConfigurationAnalyzer(PlaywrightSessionManager sessionManager)
{
    private readonly PlaywrightSessionManager _sessionManager = sessionManager;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = 32,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Result structure for Angular configuration analysis
    /// </summary>
    public class ConfigurationAnalysisResult
    {
        public bool Success { get; set; }
        public string WorkingDirectory { get; set; } = string.Empty;
        public bool AngularJsonExists { get; set; }
        public bool PackageJsonExists { get; set; }
        public bool TsConfigExists { get; set; }
        public WorkspaceConfiguration WorkspaceConfig { get; set; } = new();
        public List<ProjectConfiguration> Projects { get; set; } = [];
        public BuildConfigurations BuildConfigs { get; set; } = new();
        public DependencyAnalysis Dependencies { get; set; } = new();
        public ConfigurationValidation Validation { get; set; } = new();
        public ArchitecturalInsights Insights { get; set; } = new();
        public List<ConfigurationRecommendation> Recommendations { get; set; } = [];
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Angular workspace configuration structure
    /// </summary>
    public class WorkspaceConfiguration
    {
        public int Version { get; set; }
        public string DefaultProject { get; set; } = string.Empty;
        public int ProjectCount { get; set; }
        public List<string> ProjectNames { get; set; } = [];
        public SchemaInformation Schema { get; set; } = new();
        public GlobalSettings GlobalSettings { get; set; } = new();
        public CliConfiguration Cli { get; set; } = new();
    }

    /// <summary>
    /// Individual project configuration details
    /// </summary>
    public class ProjectConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string ProjectType { get; set; } = string.Empty; // application, library
        public string Root { get; set; } = string.Empty;
        public string SourceRoot { get; set; } = string.Empty;
        public ArchitectConfiguration Architect { get; set; } = new();
        public string Prefix { get; set; } = string.Empty;
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    /// <summary>
    /// Architect configuration for build, test, lint, etc.
    /// </summary>
    public class ArchitectConfiguration
    {
        public BuildTarget Build { get; set; } = new();
        public BuildTarget Serve { get; set; } = new();
        public BuildTarget Test { get; set; } = new();
        public BuildTarget Lint { get; set; } = new();
        public BuildTarget ExtractI18n { get; set; } = new();
        public List<CustomTarget> CustomTargets { get; set; } = [];
    }

    /// <summary>
    /// Build target configuration
    /// </summary>
    public class BuildTarget
    {
        public string Builder { get; set; } = string.Empty;
        public Dictionary<string, object> Options { get; set; } = new();
        public Dictionary<string, BuildConfiguration> Configurations { get; set; } = new();
        public List<string> DefaultConfiguration { get; set; } = [];
    }

    /// <summary>
    /// Build configuration details
    /// </summary>
    public class BuildConfiguration
    {
        public string OutputPath { get; set; } = string.Empty;
        public bool Optimization { get; set; }
        public bool SourceMap { get; set; }
        public bool ExtractCss { get; set; }
        public bool NamedChunks { get; set; }
        public bool Aot { get; set; }
        public string BudgetType { get; set; } = string.Empty;
        public List<BudgetConfig> Budgets { get; set; } = [];
        public Dictionary<string, object> AdditionalOptions { get; set; } = new();
    }

    /// <summary>
    /// Budget configuration for bundle size limits
    /// </summary>
    public class BudgetConfig
    {
        public string Type { get; set; } = string.Empty;
        public string Baseline { get; set; } = string.Empty;
        public string Maximum { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Custom architect target
    /// </summary>
    public class CustomTarget
    {
        public string Name { get; set; } = string.Empty;
        public string Builder { get; set; } = string.Empty;
        public Dictionary<string, object> Options { get; set; } = new();
    }

    /// <summary>
    /// Build configurations analysis
    /// </summary>
    public class BuildConfigurations
    {
        public List<string> AvailableConfigurations { get; set; } = [];
        public string DefaultConfiguration { get; set; } = string.Empty;
        public bool HasProduction { get; set; }
        public bool HasDevelopment { get; set; }
        public bool HasTesting { get; set; }
        public ConfigurationComparison Comparison { get; set; } = new();
        public OptimizationAnalysis Optimization { get; set; } = new();
    }

    /// <summary>
    /// Configuration comparison analysis
    /// </summary>
    public class ConfigurationComparison
    {
        public List<string> SharedOptions { get; set; } = [];
        public List<string> UniqueToProduction { get; set; } = [];
        public List<string> UniqueToDevevelopment { get; set; } = [];
        public Dictionary<string, ConfigurationDifference> Differences { get; set; } = new();
    }

    /// <summary>
    /// Configuration difference details
    /// </summary>
    public class ConfigurationDifference
    {
        public string Property { get; set; } = string.Empty;
        public object ProductionValue { get; set; } = new();
        public object DevelopmentValue { get; set; } = new();
        public string Impact { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Optimization analysis
    /// </summary>
    public class OptimizationAnalysis
    {
        public bool TreeShakingEnabled { get; set; }
        public bool AotEnabled { get; set; }
        public bool MinificationEnabled { get; set; }
        public bool CompressionEnabled { get; set; }
        public bool LazyLoadingSupported { get; set; }
        public List<string> OptimizationOpportunities { get; set; } = [];
    }

    /// <summary>
    /// Dependency analysis
    /// </summary>
    public class DependencyAnalysis
    {
        public AngularDependencies Angular { get; set; } = new();
        public List<ThirdPartyDependency> ThirdParty { get; set; } = [];
        public List<string> DevDependencies { get; set; } = [];
        public List<string> PeerDependencies { get; set; } = [];
        public SecurityAnalysis Security { get; set; } = new();
        public VersionAnalysis Versions { get; set; } = new();
    }

    /// <summary>
    /// Angular-specific dependencies
    /// </summary>
    public class AngularDependencies
    {
        public string CoreVersion { get; set; } = string.Empty;
        public string CliVersion { get; set; } = string.Empty;
        public string TypeScriptVersion { get; set; } = string.Empty;
        public string RxJsVersion { get; set; } = string.Empty;
        public List<string> AngularPackages { get; set; } = [];
        public bool StandaloneSupport { get; set; }
        public bool SignalsSupport { get; set; }
        public bool ZonelessSupport { get; set; }
    }

    /// <summary>
    /// Third-party dependency information
    /// </summary>
    public class ThirdPartyDependency
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // ui, state-management, testing, etc.
        public string Description { get; set; } = string.Empty;
        public bool IsDeprecated { get; set; }
        public List<string> Alternatives { get; set; } = [];
    }

    /// <summary>
    /// Security analysis for dependencies
    /// </summary>
    public class SecurityAnalysis
    {
        public int VulnerabilityCount { get; set; }
        public List<SecurityVulnerability> Vulnerabilities { get; set; } = [];
        public List<string> OutdatedPackages { get; set; } = [];
        public SecurityScore Score { get; set; } = new();
    }

    /// <summary>
    /// Security vulnerability details
    /// </summary>
    public class SecurityVulnerability
    {
        public string Package { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RecommendedVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Security scoring
    /// </summary>
    public class SecurityScore
    {
        public int Overall { get; set; } // 0-100
        public int DependencyHealth { get; set; }
        public int UpdateCompliance { get; set; }
        public string Risk { get; set; } = string.Empty; // low, medium, high, critical
    }

    /// <summary>
    /// Version analysis across dependencies
    /// </summary>
    public class VersionAnalysis
    {
        public bool AngularVersionsConsistent { get; set; }
        public List<string> VersionMismatches { get; set; } = [];
        public List<string> MajorVersionUpdatesAvailable { get; set; } = [];
        public CompatibilityMatrix Compatibility { get; set; } = new();
    }

    /// <summary>
    /// Compatibility matrix for Angular ecosystem
    /// </summary>
    public class CompatibilityMatrix
    {
        public bool NodeCompatible { get; set; }
        public bool TypeScriptCompatible { get; set; }
        public bool RxJsCompatible { get; set; }
        public List<string> IncompatiblePackages { get; set; } = [];
    }

    /// <summary>
    /// Configuration validation results
    /// </summary>
    public class ConfigurationValidation
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = [];
        public List<ValidationWarning> Warnings { get; set; } = [];
        public SchemaValidation Schema { get; set; } = new();
        public BestPracticesValidation BestPractices { get; set; } = new();
    }

    /// <summary>
    /// Validation error details
    /// </summary>
    public class ValidationError
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Fix { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validation warning details
    /// </summary>
    public class ValidationWarning
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Schema validation results
    /// </summary>
    public class SchemaValidation
    {
        public bool SchemaValid { get; set; }
        public string SchemaVersion { get; set; } = string.Empty;
        public List<string> SchemaViolations { get; set; } = [];
        public bool UpgradeRecommended { get; set; }
    }

    /// <summary>
    /// Best practices validation
    /// </summary>
    public class BestPracticesValidation
    {
        public int Score { get; set; } // 0-100
        public List<string> Violations { get; set; } = [];
        public List<string> Improvements { get; set; } = [];
        public PerformanceChecks Performance { get; set; } = new();
        public MaintenanceChecks Maintenance { get; set; } = new();
    }

    /// <summary>
    /// Performance-related checks
    /// </summary>
    public class PerformanceChecks
    {
        public bool BundleOptimization { get; set; }
        public bool LazyLoading { get; set; }
        public bool TreeShaking { get; set; }
        public bool SourceMaps { get; set; }
        public List<string> Recommendations { get; set; } = [];
    }

    /// <summary>
    /// Maintenance-related checks
    /// </summary>
    public class MaintenanceChecks
    {
        public bool TestingConfigured { get; set; }
        public bool LintingConfigured { get; set; }
        public bool TypeCheckingStrict { get; set; }
        public bool DependenciesUpToDate { get; set; }
        public List<string> MaintenanceIssues { get; set; } = [];
    }

    /// <summary>
    /// Architectural insights
    /// </summary>
    public class ArchitecturalInsights
    {
        public ProjectStructure Structure { get; set; } = new();
        public ModuleArchitecture Modules { get; set; } = new();
        public ScalabilityAnalysis Scalability { get; set; } = new();
        public TechnologyStack TechStack { get; set; } = new();
    }

    /// <summary>
    /// Project structure analysis
    /// </summary>
    public class ProjectStructure
    {
        public string ArchitecturePattern { get; set; } = string.Empty; // monorepo, single-project, micro-frontends
        public bool IsMonorepo { get; set; }
        public int ApplicationCount { get; set; }
        public int LibraryCount { get; set; }
        public List<string> SharedLibraries { get; set; } = [];
        public DependencyGraph Dependencies { get; set; } = new();
    }

    /// <summary>
    /// Module architecture analysis
    /// </summary>
    public class ModuleArchitecture
    {
        public bool UsesStandaloneComponents { get; set; }
        public bool UsesNgModules { get; set; }
        public bool MixedArchitecture { get; set; }
        public LazyLoadingAnalysis LazyLoading { get; set; } = new();
        public RoutingAnalysis Routing { get; set; } = new();
    }

    /// <summary>
    /// Lazy loading analysis
    /// </summary>
    public class LazyLoadingAnalysis
    {
        public bool Implemented { get; set; }
        public int LazyModuleCount { get; set; }
        public List<string> LazyRoutes { get; set; } = [];
        public List<string> Opportunities { get; set; } = [];
    }

    /// <summary>
    /// Routing analysis
    /// </summary>
    public class RoutingAnalysis
    {
        public bool RouterConfigured { get; set; }
        public bool PreloadingStrategy { get; set; }
        public bool GuardsConfigured { get; set; }
        public int RouteCount { get; set; }
    }

    /// <summary>
    /// Scalability analysis
    /// </summary>
    public class ScalabilityAnalysis
    {
        public int Score { get; set; } // 0-100
        public List<string> Strengths { get; set; } = [];
        public List<string> Concerns { get; set; } = [];
        public List<string> Recommendations { get; set; } = [];
        public GrowthPotential Growth { get; set; } = new();
    }

    /// <summary>
    /// Growth potential analysis
    /// </summary>
    public class GrowthPotential
    {
        public string TeamSize { get; set; } = string.Empty; // small, medium, large, enterprise
        public string Complexity { get; set; } = string.Empty; // simple, moderate, complex, enterprise
        public List<string> ScalingBottlenecks { get; set; } = [];
    }

    /// <summary>
    /// Technology stack analysis
    /// </summary>
    public class TechnologyStack
    {
        public string AngularVersion { get; set; } = string.Empty;
        public string TypeScriptVersion { get; set; } = string.Empty;
        public string NodeVersion { get; set; } = string.Empty;
        public List<string> UILibraries { get; set; } = [];
        public List<string> StateManagement { get; set; } = [];
        public List<string> TestingFrameworks { get; set; } = [];
        public string BuildTool { get; set; } = string.Empty;
        public TechStackRecommendations Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Technology stack recommendations
    /// </summary>
    public class TechStackRecommendations
    {
        public List<string> Upgrades { get; set; } = [];
        public List<string> Alternatives { get; set; } = [];
        public List<string> NewTechnologies { get; set; } = [];
    }

    /// <summary>
    /// Dependency graph structure
    /// </summary>
    public class DependencyGraph
    {
        public List<ProjectDependency> Projects { get; set; } = [];
        public List<CircularDependency> CircularDependencies { get; set; } = [];
        public int MaxDepth { get; set; }
        public bool HasIssues { get; set; }
    }

    /// <summary>
    /// Project dependency details
    /// </summary>
    public class ProjectDependency
    {
        public string Name { get; set; } = string.Empty;
        public List<string> DependsOn { get; set; } = [];
        public List<string> UsedBy { get; set; } = [];
        public int DepthLevel { get; set; }
    }

    /// <summary>
    /// Circular dependency information
    /// </summary>
    public class CircularDependency
    {
        public List<string> Cycle { get; set; } = [];
        public string Severity { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
    }

    /// <summary>
    /// Schema information
    /// </summary>
    public class SchemaInformation
    {
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsLatest { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Global settings from angular.json
    /// </summary>
    public class GlobalSettings
    {
        public PackageManager PackageManager { get; set; } = new();
        public Dictionary<string, object> Schematics { get; set; } = new();
        public Dictionary<string, object> Analytics { get; set; } = new();
        public Dictionary<string, object> NewProjectRoot { get; set; } = new();
    }

    /// <summary>
    /// Package manager configuration
    /// </summary>
    public class PackageManager
    {
        public string Name { get; set; } = string.Empty; // npm, yarn, pnpm
        public string Version { get; set; } = string.Empty;
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    /// <summary>
    /// CLI configuration
    /// </summary>
    public class CliConfiguration
    {
        public Dictionary<string, object> Warnings { get; set; } = new();
        public Dictionary<string, object> Analytics { get; set; } = new();
        public Dictionary<string, object> Cache { get; set; } = new();
        public Dictionary<string, object> DefaultCollection { get; set; } = new();
    }

    /// <summary>
    /// Configuration recommendation
    /// </summary>
    public class ConfigurationRecommendation
    {
        public string Type { get; set; } = string.Empty; // performance, security, maintainability, scalability
        public string Priority { get; set; } = string.Empty; // low, medium, high, critical
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Implementation { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = [];
        public Dictionary<string, object> Examples { get; set; } = new();
    }

    [McpServerTool]
    [Description("Analyze Angular workspace configuration (angular.json) with comprehensive parsing and validation")]
    public async Task<string> AnalyzeAngularJsonConfig(
        [Description("Working directory containing angular.json (defaults to current directory)")] string workingDirectory = "",
        [Description("Include detailed dependency analysis")] bool includeDependencyAnalysis = true,
        [Description("Include security vulnerability scanning")] bool includeSecurityScan = true,
        [Description("Include architectural insights")] bool includeArchitecturalInsights = true,
        [Description("Session ID for context")] string sessionId = "default")
    {
        try
        {
            // Validate session exists
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new ConfigurationAnalysisResult
                {
                    Success = false,
                    WorkingDirectory = workingDirectory,
                    ErrorMessage = $"Session {sessionId} not found"
                }, JsonOptions);
            }

            var targetDirectory = string.IsNullOrWhiteSpace(workingDirectory) 
                ? Directory.GetCurrentDirectory() 
                : workingDirectory;

            var result = await AnalyzeWorkspaceConfiguration(
                targetDirectory, 
                includeDependencyAnalysis, 
                includeSecurityScan, 
                includeArchitecturalInsights);

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorResult = new ConfigurationAnalysisResult
            {
                Success = false,
                WorkingDirectory = workingDirectory,
                ErrorMessage = $"Failed to analyze Angular configuration: {ex.Message}"
            };
            
            return JsonSerializer.Serialize(errorResult, JsonOptions);
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
            var angularJsonPath = Path.Combine(directory, "angular.json");
            var packageJsonPath = Path.Combine(directory, "package.json");
            var tsConfigPath = Path.Combine(directory, "tsconfig.json");

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
            var angularJsonContent = await File.ReadAllTextAsync(angularJsonPath);
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
            if (angularConfig.TryGetProperty("version", out var version))
            {
                workspace.Version = version.GetInt32();
            }

            // Extract default project
            if (angularConfig.TryGetProperty("defaultProject", out var defaultProject))
            {
                workspace.DefaultProject = defaultProject.GetString() ?? string.Empty;
            }

            // Extract projects
            if (angularConfig.TryGetProperty("projects", out var projects))
            {
                workspace.ProjectCount = projects.EnumerateObject().Count();
                workspace.ProjectNames = projects.EnumerateObject()
                    .Select(p => p.Name)
                    .ToList();
            }

            // Extract schema information
            if (angularConfig.TryGetProperty("$schema", out var schema))
            {
                workspace.Schema = new SchemaInformation
                {
                    Url = schema.GetString() ?? string.Empty,
                    Version = ExtractSchemaVersion(schema.GetString() ?? string.Empty)
                };
            }

            // Extract CLI configuration
            if (angularConfig.TryGetProperty("cli", out var cli))
            {
                workspace.Cli = ExtractCliConfiguration(cli);
            }

            // Extract schematics
            if (angularConfig.TryGetProperty("schematics", out var schematics))
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

            if (angularConfig.TryGetProperty("projects", out var projectsElement))
            {
                foreach (var project in projectsElement.EnumerateObject())
                {
                    var projectConfig = new ProjectConfiguration
                    {
                        Name = project.Name
                    };

                    var projectValue = project.Value;

                    // Extract project type
                    if (projectValue.TryGetProperty("projectType", out var projectType))
                    {
                        projectConfig.ProjectType = projectType.GetString() ?? string.Empty;
                    }

                    // Extract root and source root
                    if (projectValue.TryGetProperty("root", out var root))
                    {
                        projectConfig.Root = root.GetString() ?? string.Empty;
                    }

                    if (projectValue.TryGetProperty("sourceRoot", out var sourceRoot))
                    {
                        projectConfig.SourceRoot = sourceRoot.GetString() ?? string.Empty;
                    }

                    // Extract prefix
                    if (projectValue.TryGetProperty("prefix", out var prefix))
                    {
                        projectConfig.Prefix = prefix.GetString() ?? string.Empty;
                    }

                    // Extract architect configuration
                    if (projectValue.TryGetProperty("architect", out var architect))
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

            if (angularConfig.TryGetProperty("projects", out var projects))
            {
                foreach (var project in projects.EnumerateObject())
                {
                    if (project.Value.TryGetProperty("architect", out var architect) &&
                        architect.TryGetProperty("build", out var build) &&
                        build.TryGetProperty("configurations", out var configurations))
                    {
                        foreach (var config in configurations.EnumerateObject())
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
            var packageJsonContent = await File.ReadAllTextAsync(packageJsonPath);
            var packageJson = JsonSerializer.Deserialize<JsonElement>(packageJsonContent);

            // Analyze Angular dependencies
            dependencies.Angular = await AnalyzeAngularDependencies(packageJson);

            // Analyze third-party dependencies
            dependencies.ThirdParty = await AnalyzeThirdPartyDependencies(packageJson);

            // Extract dev dependencies
            if (packageJson.TryGetProperty("devDependencies", out var devDeps))
            {
                dependencies.DevDependencies = devDeps.EnumerateObject()
                    .Select(d => $"{d.Name}@{d.Value.GetString()}")
                    .ToList();
            }

            // Extract peer dependencies
            if (packageJson.TryGetProperty("peerDependencies", out var peerDeps))
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
            var insights = new ArchitecturalInsights();

            // Analyze project structure
            insights.Structure = AnalyzeProjectStructure(angularConfig);

            // Analyze module architecture
            insights.Modules = AnalyzeModuleArchitecture(angularConfig, directory);

            // Analyze scalability
            insights.Scalability = AnalyzeScalability(angularConfig);

            // Analyze technology stack
            insights.TechStack = AnalyzeTechnologyStack(angularConfig);

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
        var match = System.Text.RegularExpressions.Regex.Match(schemaUrl, versionPattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static CliConfiguration ExtractCliConfiguration(JsonElement cli)
    {
        var config = new CliConfiguration();

        if (cli.TryGetProperty("warnings", out var warnings))
        {
            config.Warnings = JsonElementToDictionary(warnings);
        }

        if (cli.TryGetProperty("analytics", out var analytics))
        {
            config.Analytics = JsonElementToDictionary(analytics);
        }

        if (cli.TryGetProperty("cache", out var cache))
        {
            config.Cache = JsonElementToDictionary(cache);
        }

        return config;
    }

    private static ArchitectConfiguration ExtractArchitectConfiguration(JsonElement architect)
    {
        var config = new ArchitectConfiguration();

        if (architect.TryGetProperty("build", out var build))
        {
            config.Build = ExtractBuildTarget(build);
        }

        if (architect.TryGetProperty("serve", out var serve))
        {
            config.Serve = ExtractBuildTarget(serve);
        }

        if (architect.TryGetProperty("test", out var test))
        {
            config.Test = ExtractBuildTarget(test);
        }

        if (architect.TryGetProperty("lint", out var lint))
        {
            config.Lint = ExtractBuildTarget(lint);
        }

        if (architect.TryGetProperty("extract-i18n", out var extractI18n))
        {
            config.ExtractI18n = ExtractBuildTarget(extractI18n);
        }

        // Extract custom targets
        config.CustomTargets = architect.EnumerateObject()
            .Where(p => !new[] { "build", "serve", "test", "lint", "extract-i18n" }.Contains(p.Name))
            .Select(p => new CustomTarget
            {
                Name = p.Name,
                Builder = p.Value.TryGetProperty("builder", out var builder) ? builder.GetString() ?? string.Empty : string.Empty,
                Options = p.Value.TryGetProperty("options", out var options) ? JsonElementToDictionary(options) : new Dictionary<string, object>()
            })
            .ToList();

        return config;
    }

    private static BuildTarget ExtractBuildTarget(JsonElement target)
    {
        var buildTarget = new BuildTarget();

        if (target.TryGetProperty("builder", out var builder))
        {
            buildTarget.Builder = builder.GetString() ?? string.Empty;
        }

        if (target.TryGetProperty("options", out var options))
        {
            buildTarget.Options = JsonElementToDictionary(options);
        }

        if (target.TryGetProperty("configurations", out var configurations))
        {
            buildTarget.Configurations = configurations.EnumerateObject()
                .ToDictionary(
                    c => c.Name,
                    c => ExtractBuildConfiguration(c.Value)
                );
        }

        if (target.TryGetProperty("defaultConfiguration", out var defaultConfig))
        {
            buildTarget.DefaultConfiguration = [defaultConfig.GetString() ?? string.Empty];
        }

        return buildTarget;
    }

    private static BuildConfiguration ExtractBuildConfiguration(JsonElement config)
    {
        var buildConfig = new BuildConfiguration();

        if (config.TryGetProperty("outputPath", out var outputPath))
        {
            buildConfig.OutputPath = outputPath.GetString() ?? string.Empty;
        }

        if (config.TryGetProperty("optimization", out var optimization))
        {
            buildConfig.Optimization = optimization.GetBoolean();
        }

        if (config.TryGetProperty("sourceMap", out var sourceMap))
        {
            buildConfig.SourceMap = sourceMap.GetBoolean();
        }

        if (config.TryGetProperty("extractCss", out var extractCss))
        {
            buildConfig.ExtractCss = extractCss.GetBoolean();
        }

        if (config.TryGetProperty("namedChunks", out var namedChunks))
        {
            buildConfig.NamedChunks = namedChunks.GetBoolean();
        }

        if (config.TryGetProperty("aot", out var aot))
        {
            buildConfig.Aot = aot.GetBoolean();
        }

        if (config.TryGetProperty("budgets", out var budgets))
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

        if (budget.TryGetProperty("type", out var type))
        {
            budgetConfig.Type = type.GetString() ?? string.Empty;
        }

        if (budget.TryGetProperty("baseline", out var baseline))
        {
            budgetConfig.Baseline = baseline.GetString() ?? string.Empty;
        }

        if (budget.TryGetProperty("maximumWarning", out var maxWarning))
        {
            budgetConfig.Warning = maxWarning.GetString() ?? string.Empty;
        }

        if (budget.TryGetProperty("maximumError", out var maxError))
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
        if (angularConfig.TryGetProperty("projects", out var projects))
        {
            foreach (var project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out var architect) &&
                    architect.TryGetProperty("build", out var build))
                {
                    // Check production configuration
                    if (build.TryGetProperty("configurations", out var configurations) &&
                        configurations.TryGetProperty("production", out var production))
                    {
                        if (production.TryGetProperty("optimization", out var opt))
                        {
                            optimization.MinificationEnabled = opt.GetBoolean();
                        }

                        if (production.TryGetProperty("aot", out var aot))
                        {
                            optimization.AotEnabled = aot.GetBoolean();
                        }

                        if (production.TryGetProperty("buildOptimizer", out var buildOpt))
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

            if (packageJson.TryGetProperty("dependencies", out var deps))
            {
                foreach (var dep in deps.EnumerateObject())
                {
                    var name = dep.Name;
                    var version = dep.Value.GetString() ?? string.Empty;

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

            if (packageJson.TryGetProperty("dependencies", out var deps))
            {
                foreach (var dep in deps.EnumerateObject())
                {
                    var name = dep.Name;
                    var version = dep.Value.GetString() ?? string.Empty;

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
            var security = new SecurityAnalysis();
            
            // This would integrate with security vulnerability databases
            // For now, provide a basic analysis structure
            
            security.Score = new SecurityScore
            {
                Overall = 85, // Example score
                DependencyHealth = 90,
                UpdateCompliance = 80,
                Risk = "low"
            };

            return security;
        });
    }

    private async Task<VersionAnalysis> AnalyzeVersionCompatibility(JsonElement packageJson)
    {
        return await Task.Run(() =>
        {
            var versions = new VersionAnalysis();
            
            // Analyze Angular package version consistency
            versions.AngularVersionsConsistent = CheckAngularVersionConsistency(packageJson);
            
            // Check for major version updates available
            versions.MajorVersionUpdatesAvailable = GetMajorUpdatesAvailable(packageJson);
            
            // Analyze compatibility matrix
            versions.Compatibility = AnalyzeCompatibilityMatrix(packageJson);

            return versions;
        });
    }

    private static SchemaValidation ValidateSchema(JsonElement angularConfig)
    {
        var schema = new SchemaValidation
        {
            SchemaValid = true // Basic validation - could be enhanced with actual schema validation
        };

        if (angularConfig.TryGetProperty("$schema", out var schemaElement))
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
        var testingConfigured = HasTestingConfiguration(angularConfig);
        bestPractices.Maintenance.TestingConfigured = testingConfigured;
        if (!testingConfigured)
        {
            violations.Add("No testing configuration found");
            score -= 15;
        }

        // Check for linting configuration
        var lintingConfigured = HasLintingConfiguration(angularConfig);
        bestPractices.Maintenance.LintingConfigured = lintingConfigured;
        if (!lintingConfigured)
        {
            violations.Add("No linting configuration found");
            score -= 10;
        }

        // Check for strict TypeScript configuration
        var strictTypeScript = HasStrictTypeScript(directory);
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
        if (angularConfig.TryGetProperty("$schema", out var schema))
        {
            var schemaUrl = schema.GetString() ?? string.Empty;
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

        if (angularConfig.TryGetProperty("projects", out var projects))
        {
            var projectList = projects.EnumerateObject().ToList();
            structure.ApplicationCount = projectList.Count(p => 
                p.Value.TryGetProperty("projectType", out var type) && 
                type.GetString() == "application");
            
            structure.LibraryCount = projectList.Count(p => 
                p.Value.TryGetProperty("projectType", out var type) && 
                type.GetString() == "library");

            structure.IsMonorepo = projectList.Count > 1;
            structure.ArchitecturePattern = structure.IsMonorepo ? "monorepo" : "single-project";
        }

        return structure;
    }

    private static ModuleArchitecture AnalyzeModuleArchitecture(JsonElement angularConfig, string directory)
    {
        var modules = new ModuleArchitecture();

        // This would require analyzing the actual source files
        // For now, provide basic structure
        modules.UsesNgModules = true; // Default assumption
        modules.UsesStandaloneComponents = false; // Would need to scan source files

        return modules;
    }

    private static ScalabilityAnalysis AnalyzeScalability(JsonElement angularConfig)
    {
        var scalability = new ScalabilityAnalysis();
        var score = 100;
        var strengths = new List<string>();
        var concerns = new List<string>();

        // Analyze project count and complexity
        if (angularConfig.TryGetProperty("projects", out var projects))
        {
            var projectCount = projects.EnumerateObject().Count();
            
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
        if (angularConfig.TryGetProperty("projects", out var projects))
        {
            foreach (var project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out var architect) &&
                    architect.TryGetProperty("build", out var build) &&
                    build.TryGetProperty("builder", out var builder))
                {
                    var builderName = builder.GetString() ?? string.Empty;
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
            foreach (var property in element.EnumerateObject())
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
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
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

        if (packageJson.TryGetProperty("dependencies", out var deps))
        {
            foreach (var dep in deps.EnumerateObject())
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
        if (angularConfig.TryGetProperty("projects", out var projects))
        {
            foreach (var project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out var architect) &&
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
        if (angularConfig.TryGetProperty("projects", out var projects))
        {
            foreach (var project in projects.EnumerateObject())
            {
                if (project.Value.TryGetProperty("architect", out var architect) &&
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
            var tsConfigPath = Path.Combine(directory, "tsconfig.json");
            if (!File.Exists(tsConfigPath))
                return false;

            var content = File.ReadAllText(tsConfigPath);
            var tsConfig = JsonSerializer.Deserialize<JsonElement>(content);

            if (tsConfig.TryGetProperty("compilerOptions", out var compilerOptions) &&
                compilerOptions.TryGetProperty("strict", out var strict))
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
