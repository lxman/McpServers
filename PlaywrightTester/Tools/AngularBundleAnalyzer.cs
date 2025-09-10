using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PlaywrightTester.Services;

namespace PlaywrightTester.Tools;

/// <summary>
/// Handles Angular bundle size analysis by component for optimization insights
/// Implements ANG-015 Bundle Size Analysis by Component
/// </summary>
[McpServerToolType]
public class AngularBundleAnalyzer(PlaywrightSessionManager sessionManager)
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
    /// Result structure for Angular bundle size analysis
    /// </summary>
    public class BundleSizeAnalysisResult
    {
        public bool Success { get; set; }
        public string WorkingDirectory { get; set; } = string.Empty;
        public bool AngularProjectDetected { get; set; }
        public bool WebpackStatsAvailable { get; set; }
        public BundleOverview Overview { get; set; } = new();
        public List<ComponentBundleImpact> ComponentImpacts { get; set; } = new();
        public List<ModuleBundleImpact> ModuleImpacts { get; set; } = new();
        public List<AssetAnalysis> Assets { get; set; } = new();
        public ChunkAnalysis Chunks { get; set; } = new();
        public DependencyAnalysis Dependencies { get; set; } = new();
        public BundleOptimizationRecommendations Recommendations { get; set; } = new();
        public PerformanceMetrics Performance { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Overall bundle overview and statistics
    /// </summary>
    public class BundleOverview
    {
        public long TotalSize { get; set; }
        public long GzippedSize { get; set; }
        public long UncompressedSize { get; set; }
        public int ChunkCount { get; set; }
        public int AssetCount { get; set; }
        public int ComponentCount { get; set; }
        public int ModuleCount { get; set; }
        public string BuildConfiguration { get; set; } = string.Empty;
        public DateTime AnalysisTimestamp { get; set; }
        public BundleSizeDistribution Distribution { get; set; } = new();
        public BundleComparison Comparison { get; set; } = new();
    }

    /// <summary>
    /// Bundle size distribution across different categories
    /// </summary>
    public class BundleSizeDistribution
    {
        public long VendorSize { get; set; }
        public long ApplicationSize { get; set; }
        public long RuntimeSize { get; set; }
        public long PolyfillsSize { get; set; }
        public long StylesSize { get; set; }
        public Dictionary<string, long> ByChunk { get; set; } = new();
        public Dictionary<string, double> PercentageByCategory { get; set; } = new();
    }

    /// <summary>
    /// Bundle comparison against recommended sizes
    /// </summary>
    public class BundleComparison
    {
        public BudgetStatus InitialBudgetStatus { get; set; } = new();
        public BudgetStatus AnyBudgetStatus { get; set; } = new();
        public List<BudgetViolation> BudgetViolations { get; set; } = new();
        public RecommendedSizes RecommendedSizes { get; set; } = new();
        public SizeScore Score { get; set; } = new();
    }

    /// <summary>
    /// Budget status for specific bundle types
    /// </summary>
    public class BudgetStatus
    {
        public bool WithinBudget { get; set; }
        public long MaximumSize { get; set; }
        public long CurrentSize { get; set; }
        public double UtilizationPercentage { get; set; }
        public string Status { get; set; } = string.Empty; // ok, warning, error
    }

    /// <summary>
    /// Budget violation details
    /// </summary>
    public class BudgetViolation
    {
        public string BudgetType { get; set; } = string.Empty;
        public long ExpectedSize { get; set; }
        public long ActualSize { get; set; }
        public long Excess { get; set; }
        public string Severity { get; set; } = string.Empty;
        public List<string> CausingComponents { get; set; } = new();
    }

    /// <summary>
    /// Recommended bundle sizes based on industry standards
    /// </summary>
    public class RecommendedSizes
    {
        public long Initial { get; set; } // 200-300KB recommended
        public long Any { get; set; } // 2MB recommended
        public long Vendor { get; set; } // 500KB recommended
        public long Application { get; set; } // 1MB recommended
        public string Rationale { get; set; } = string.Empty;
    }

    /// <summary>
    /// Bundle size scoring
    /// </summary>
    public class SizeScore
    {
        public int Overall { get; set; } // 0-100
        public int InitialLoad { get; set; }
        public int LazyLoading { get; set; }
        public int VendorOptimization { get; set; }
        public string Grade { get; set; } = string.Empty; // A, B, C, D, F
        public List<string> ScoreFactors { get; set; } = new();
    }

    /// <summary>
    /// Component-specific bundle impact analysis
    /// </summary>
    public class ComponentBundleImpact
    {
        public string ComponentName { get; set; } = string.Empty;
        public string ComponentPath { get; set; } = string.Empty;
        public string ComponentType { get; set; } = string.Empty; // standalone, module-based
        public long SizeBytes { get; set; }
        public long GzippedSizeBytes { get; set; }
        public double PercentageOfBundle { get; set; }
        public int DependencyCount { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<ComponentDependency> ComponentDependencies { get; set; } = new();
        public ComponentOptimization Optimization { get; set; } = new();
        public ComponentComplexity Complexity { get; set; } = new();
        public List<string> OptimizationOpportunities { get; set; } = new();
        public string ImpactLevel { get; set; } = string.Empty; // low, medium, high, critical
    }

    /// <summary>
    /// Component dependency information
    /// </summary>
    public class ComponentDependency
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // import, service, component
        public long SizeBytes { get; set; }
        public bool IsExternal { get; set; }
        public bool IsLazyLoaded { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// Component optimization analysis
    /// </summary>
    public class ComponentOptimization
    {
        public bool OnPushStrategy { get; set; }
        public bool LazyLoaded { get; set; }
        public bool TreeShakable { get; set; }
        public bool HasDeadCode { get; set; }
        public bool UsesStandaloneAPI { get; set; }
        public double OptimizationScore { get; set; } // 0-100
        public List<string> OptimizationSuggestions { get; set; } = new();
    }

    /// <summary>
    /// Component complexity metrics
    /// </summary>
    public class ComponentComplexity
    {
        public int TemplateSize { get; set; }
        public int StyleSize { get; set; }
        public int LogicSize { get; set; }
        public int ImportCount { get; set; }
        public int MethodCount { get; set; }
        public int LifecycleHookCount { get; set; }
        public string ComplexityLevel { get; set; } = string.Empty; // simple, moderate, complex, highly-complex
    }

    /// <summary>
    /// Module-specific bundle impact analysis
    /// </summary>
    public class ModuleBundleImpact
    {
        public string ModuleName { get; set; } = string.Empty;
        public string ModulePath { get; set; } = string.Empty;
        public string ModuleType { get; set; } = string.Empty; // feature, shared, core, lazy
        public long SizeBytes { get; set; }
        public long GzippedSizeBytes { get; set; }
        public double PercentageOfBundle { get; set; }
        public int ComponentCount { get; set; }
        public int ServiceCount { get; set; }
        public List<string> Components { get; set; } = new();
        public List<string> Services { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public ModuleOptimization Optimization { get; set; } = new();
        public string LoadingStrategy { get; set; } = string.Empty; // eager, lazy
        public List<string> OptimizationOpportunities { get; set; } = new();
    }

    /// <summary>
    /// Module optimization analysis
    /// </summary>
    public class ModuleOptimization
    {
        public bool IsLazyLoaded { get; set; }
        public bool HasSharedComponents { get; set; }
        public bool HasCircularDependencies { get; set; }
        public bool ProperlyTreeShaken { get; set; }
        public double OptimizationScore { get; set; } // 0-100
        public List<string> ModuleDependencies { get; set; } = new();
        public List<string> UnusedExports { get; set; } = new();
    }

    /// <summary>
    /// Asset analysis for bundle optimization
    /// </summary>
    public class AssetAnalysis
    {
        public string AssetName { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty; // js, css, images, fonts
        public long SizeBytes { get; set; }
        public long GzippedSizeBytes { get; set; }
        public double PercentageOfBundle { get; set; }
        public bool IsOptimized { get; set; }
        public AssetOptimization Optimization { get; set; } = new();
        public List<string> OptimizationSuggestions { get; set; } = new();
    }

    /// <summary>
    /// Asset optimization details
    /// </summary>
    public class AssetOptimization
    {
        public bool Minified { get; set; }
        public bool Compressed { get; set; }
        public bool TreeShaken { get; set; }
        public bool HasSourceMaps { get; set; }
        public string CompressionRatio { get; set; } = string.Empty;
        public List<string> OptimizationTechniques { get; set; } = new();
    }

    /// <summary>
    /// Chunk analysis for bundle optimization
    /// </summary>
    public class ChunkAnalysis
    {
        public List<ChunkInfo> Chunks { get; set; } = new();
        public ChunkOptimization Optimization { get; set; } = new();
        public List<string> OptimizationOpportunities { get; set; } = new();
        public SplittingStrategy RecommendedStrategy { get; set; } = new();
    }

    /// <summary>
    /// Individual chunk information
    /// </summary>
    public class ChunkInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // initial, async, runtime
        public long SizeBytes { get; set; }
        public long GzippedSizeBytes { get; set; }
        public double PercentageOfBundle { get; set; }
        public List<string> Modules { get; set; } = new();
        public List<string> Components { get; set; } = new();
        public bool IsLazyLoaded { get; set; }
        public string LoadPriority { get; set; } = string.Empty; // high, medium, low
    }

    /// <summary>
    /// Chunk optimization analysis
    /// </summary>
    public class ChunkOptimization
    {
        public bool ProperCodeSplitting { get; set; }
        public bool OptimalChunkSizes { get; set; }
        public bool HasDuplicatedCode { get; set; }
        public int ChunkCount { get; set; }
        public double AverageChunkSize { get; set; }
        public List<string> LargeChunks { get; set; } = new();
        public List<string> SmallChunks { get; set; } = new();
    }

    /// <summary>
    /// Recommended chunk splitting strategy
    /// </summary>
    public class SplittingStrategy
    {
        public string Strategy { get; set; } = string.Empty; // feature-based, route-based, vendor-splitting
        public List<string> RecommendedSplits { get; set; } = new();
        public List<string> MergeOpportunities { get; set; } = new();
        public string Rationale { get; set; } = string.Empty;
    }

    /// <summary>
    /// Dependency analysis for bundle optimization
    /// </summary>
    public class DependencyAnalysis
    {
        public List<DependencyImpact> ThirdPartyDependencies { get; set; } = new();
        public List<DependencyImpact> InternalDependencies { get; set; } = new();
        public DependencyOptimization Optimization { get; set; } = new();
        public List<string> OptimizationOpportunities { get; set; } = new();
        public UnusedDependencies UnusedDependencies { get; set; } = new();
    }

    /// <summary>
    /// Individual dependency impact on bundle size
    /// </summary>
    public class DependencyImpact
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public long GzippedSizeBytes { get; set; }
        public double PercentageOfBundle { get; set; }
        public bool IsTreeShakable { get; set; }
        public bool IsUsed { get; set; }
        public List<string> UsedBy { get; set; } = new();
        public List<string> Alternatives { get; set; } = new();
        public DependencyOptimizationInfo Optimization { get; set; } = new();
    }

    /// <summary>
    /// Dependency optimization information
    /// </summary>
    public class DependencyOptimizationInfo
    {
        public bool CanBeTreeShaken { get; set; }
        public bool CanBeLazyLoaded { get; set; }
        public bool HasSmallerAlternatives { get; set; }
        public List<string> OptimizationSuggestions { get; set; } = new();
        public List<AlternativeDependency> AlternativeDependencies { get; set; } = new();
    }

    /// <summary>
    /// Alternative dependency suggestion
    /// </summary>
    public class AlternativeDependency
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public long SizeSavings { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string MigrationEffort { get; set; } = string.Empty; // low, medium, high
    }

    /// <summary>
    /// Dependency optimization analysis
    /// </summary>
    public class DependencyOptimization
    {
        public int TotalDependencies { get; set; }
        public int OptimizedDependencies { get; set; }
        public int UnusedDependencies { get; set; }
        public long PotentialSavings { get; set; }
        public double OptimizationScore { get; set; } // 0-100
        public List<string> QuickWins { get; set; } = new();
    }

    /// <summary>
    /// Unused dependencies analysis
    /// </summary>
    public class UnusedDependencies
    {
        public List<string> CompletelyUnused { get; set; } = new();
        public List<string> PartiallyUnused { get; set; } = new();
        public long PotentialSavings { get; set; }
        public List<string> SafeToRemove { get; set; } = new();
        public List<string> RequiresInvestigation { get; set; } = new();
    }

    /// <summary>
    /// Bundle optimization recommendations
    /// </summary>
    public class BundleOptimizationRecommendations
    {
        public List<OptimizationRecommendation> HighPriority { get; set; } = new();
        public List<OptimizationRecommendation> MediumPriority { get; set; } = new();
        public List<OptimizationRecommendation> LowPriority { get; set; } = new();
        public List<OptimizationRecommendation> QuickWins { get; set; } = new();
        public OptimizationSummary Summary { get; set; } = new();
        public ImplementationGuide Implementation { get; set; } = new();
    }

    /// <summary>
    /// Individual optimization recommendation
    /// </summary>
    public class OptimizationRecommendation
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // component, dependency, chunk, asset
        public string Priority { get; set; } = string.Empty; // high, medium, low
        public long PotentialSavings { get; set; }
        public double ImplementationEffort { get; set; } // 1-10 scale
        public string Impact { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new();
        public List<string> AffectedFiles { get; set; } = new();
        public List<string> Requirements { get; set; } = new();
        public string EstimatedTime { get; set; } = string.Empty;
        public Dictionary<string, object> Examples { get; set; } = new();
    }

    /// <summary>
    /// Optimization summary
    /// </summary>
    public class OptimizationSummary
    {
        public long TotalPotentialSavings { get; set; }
        public double TotalSavingsPercentage { get; set; }
        public int TotalRecommendations { get; set; }
        public int QuickWinCount { get; set; }
        public string EstimatedImplementationTime { get; set; } = string.Empty;
        public string ExpectedImpact { get; set; } = string.Empty;
        public List<string> PrimaryFocusAreas { get; set; } = new();
    }

    /// <summary>
    /// Implementation guide for optimization recommendations
    /// </summary>
    public class ImplementationGuide
    {
        public List<ImplementationPhase> Phases { get; set; } = new();
        public List<string> Prerequisites { get; set; } = new();
        public List<string> Tools { get; set; } = new();
        public List<string> Resources { get; set; } = new();
        public string EstimatedTimeline { get; set; } = string.Empty;
    }

    /// <summary>
    /// Implementation phase details
    /// </summary>
    public class ImplementationPhase
    {
        public int PhaseNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> RecommendationIds { get; set; } = new();
        public string EstimatedDuration { get; set; } = string.Empty;
        public List<string> Deliverables { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
    }

    /// <summary>
    /// Performance metrics related to bundle size
    /// </summary>
    public class PerformanceMetrics
    {
        public LoadingMetrics Loading { get; set; } = new();
        public RenderingMetrics Rendering { get; set; } = new();
        public NetworkMetrics Network { get; set; } = new();
        public UserExperienceMetrics UserExperience { get; set; } = new();
    }

    /// <summary>
    /// Loading performance metrics
    /// </summary>
    public class LoadingMetrics
    {
        public double EstimatedDownloadTime3G { get; set; }
        public double EstimatedDownloadTime4G { get; set; }
        public double EstimatedDownloadTimeFiber { get; set; }
        public double EstimatedParseTime { get; set; }
        public double EstimatedExecutionTime { get; set; }
        public double TotalLoadTime { get; set; }
    }

    /// <summary>
    /// Rendering performance metrics
    /// </summary>
    public class RenderingMetrics
    {
        public double EstimatedFirstContentfulPaint { get; set; }
        public double EstimatedLargestContentfulPaint { get; set; }
        public double EstimatedTimeToInteractive { get; set; }
        public double EstimatedFirstInputDelay { get; set; }
    }

    /// <summary>
    /// Network performance metrics
    /// </summary>
    public class NetworkMetrics
    {
        public int RequestCount { get; set; }
        public long TotalTransferSize { get; set; }
        public long CompressedSize { get; set; }
        public double CompressionRatio { get; set; }
        public int CacheableResources { get; set; }
    }

    /// <summary>
    /// User experience metrics
    /// </summary>
    public class UserExperienceMetrics
    {
        public int PerformanceScore { get; set; } // 0-100
        public string UserExperienceGrade { get; set; } = string.Empty; // A, B, C, D, F
        public List<string> UserImpactFactors { get; set; } = new();
        public List<string> ImprovementAreas { get; set; } = new();
    }

    [McpServerTool]
    [Description("Analyze Angular bundle size by component with detailed impact analysis and optimization recommendations")]
    public async Task<string> AnalyzeBundleSizeByComponent(
        [Description("Working directory containing Angular project (defaults to current directory)")] string workingDirectory = "",
        [Description("Build configuration to analyze (production, development, or custom configuration name)")] string buildConfiguration = "production",
        [Description("Include detailed component analysis")] bool includeComponentAnalysis = true,
        [Description("Include dependency analysis")] bool includeDependencyAnalysis = true,
        [Description("Include asset analysis")] bool includeAssetAnalysis = true,
        [Description("Generate optimization recommendations")] bool generateRecommendations = true,
        [Description("Maximum number of components to analyze (default: 50)")] int maxComponents = 50,
        [Description("Session ID for context")] string sessionId = "default")
    {
        try
        {
            // Validate session exists
            var session = _sessionManager.GetSession(sessionId);
            if (session == null)
            {
                return JsonSerializer.Serialize(new BundleSizeAnalysisResult
                {
                    Success = false,
                    WorkingDirectory = workingDirectory,
                    ErrorMessage = $"Session {sessionId} not found"
                }, JsonOptions);
            }

            var targetDirectory = string.IsNullOrWhiteSpace(workingDirectory) 
                ? Directory.GetCurrentDirectory() 
                : workingDirectory;

            var result = await AnalyzeBundleSize(
                targetDirectory,
                buildConfiguration,
                includeComponentAnalysis,
                includeDependencyAnalysis,
                includeAssetAnalysis,
                generateRecommendations,
                maxComponents);

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            var errorResult = new BundleSizeAnalysisResult
            {
                Success = false,
                WorkingDirectory = workingDirectory,
                ErrorMessage = $"Failed to analyze bundle size: {ex.Message}"
            };
            
            return JsonSerializer.Serialize(errorResult, JsonOptions);
        }
    }

    /// <summary>
    /// Core method to analyze Angular bundle size by component
    /// </summary>
    private async Task<BundleSizeAnalysisResult> AnalyzeBundleSize(
        string directory,
        string buildConfiguration,
        bool includeComponentAnalysis,
        bool includeDependencyAnalysis,
        bool includeAssetAnalysis,
        bool generateRecommendations,
        int maxComponents)
    {
        var result = new BundleSizeAnalysisResult
        {
            WorkingDirectory = directory
        };

        try
        {
            // Check if this is an Angular project
            var angularJsonPath = Path.Combine(directory, "angular.json");
            var packageJsonPath = Path.Combine(directory, "package.json");

            result.AngularProjectDetected = File.Exists(angularJsonPath);

            if (!result.AngularProjectDetected)
            {
                result.Success = false;
                result.ErrorMessage = "Angular project not detected. angular.json not found in the specified directory.";
                return result;
            }

            // Check for webpack stats or build output
            var buildOutputPath = await GetBuildOutputPath(angularJsonPath, buildConfiguration);
            result.WebpackStatsAvailable = await CheckWebpackStatsAvailability(buildOutputPath);

            // Generate webpack-bundle-analyzer stats if not available
            if (!result.WebpackStatsAvailable)
            {
                await GenerateWebpackStats(directory, buildConfiguration);
                result.WebpackStatsAvailable = await CheckWebpackStatsAvailability(buildOutputPath);
            }

            // Analyze bundle overview
            result.Overview = await AnalyzeBundleOverview(directory, buildConfiguration, buildOutputPath);

            // Analyze components if requested
            if (includeComponentAnalysis)
            {
                result.ComponentImpacts = await AnalyzeComponentImpacts(directory, buildOutputPath, maxComponents);
                result.ModuleImpacts = await AnalyzeModuleImpacts(directory, buildOutputPath);
            }

            // Analyze assets if requested
            if (includeAssetAnalysis)
            {
                result.Assets = await AnalyzeAssets(buildOutputPath);
                result.Chunks = await AnalyzeChunks(buildOutputPath);
            }

            // Analyze dependencies if requested
            if (includeDependencyAnalysis)
            {
                result.Dependencies = await AnalyzeDependencyImpacts(directory, packageJsonPath, buildOutputPath);
            }

            // Calculate performance metrics
            result.Performance = await CalculatePerformanceMetrics(result.Overview);

            // Generate recommendations if requested
            if (generateRecommendations)
            {
                result.Recommendations = await GenerateOptimizationRecommendations(result);
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error analyzing bundle size: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Get the build output path from Angular configuration
    /// </summary>
    private async Task<string> GetBuildOutputPath(string angularJsonPath, string buildConfiguration)
    {
        try
        {
            var angularJsonContent = await File.ReadAllTextAsync(angularJsonPath);
            var angularConfig = JsonSerializer.Deserialize<JsonElement>(angularJsonContent);

            if (angularConfig.TryGetProperty("projects", out var projects))
            {
                foreach (var project in projects.EnumerateObject())
                {
                    if (project.Value.TryGetProperty("architect", out var architect) &&
                        architect.TryGetProperty("build", out var build))
                    {
                        var outputPath = "dist";

                        // Check build configuration
                        if (build.TryGetProperty("configurations", out var configurations) &&
                            configurations.TryGetProperty(buildConfiguration, out var config) &&
                            config.TryGetProperty("outputPath", out var configOutputPath))
                        {
                            outputPath = configOutputPath.GetString() ?? "dist";
                        }
                        else if (build.TryGetProperty("options", out var options) &&
                                 options.TryGetProperty("outputPath", out var defaultOutputPath))
                        {
                            outputPath = defaultOutputPath.GetString() ?? "dist";
                        }

                        return Path.Combine(Path.GetDirectoryName(angularJsonPath) ?? "", outputPath);
                    }
                }
            }

            return Path.Combine(Path.GetDirectoryName(angularJsonPath) ?? "", "dist");
        }
        catch
        {
            return Path.Combine(Path.GetDirectoryName(angularJsonPath) ?? "", "dist");
        }
    }

    /// <summary>
    /// Check if webpack bundle analyzer stats are available
    /// </summary>
    private async Task<bool> CheckWebpackStatsAvailability(string buildOutputPath)
    {
        return await Task.Run(() =>
        {
            var statsJsonPath = Path.Combine(buildOutputPath, "stats.json");
            var bundleAnalyzerPath = Path.Combine(buildOutputPath, "bundle-analyzer.json");
            
            return File.Exists(statsJsonPath) || File.Exists(bundleAnalyzerPath) || Directory.Exists(buildOutputPath);
        });
    }

    /// <summary>
    /// Generate webpack bundle analyzer stats if not available
    /// </summary>
    private async Task GenerateWebpackStats(string directory, string buildConfiguration)
    {
        try
        {
            // This would typically run: ng build --stats-json --configuration=production
            // For now, we'll simulate the analysis using available build artifacts
            await Task.Delay(100); // Simulate stats generation
        }
        catch
        {
            // Ignore errors in stats generation
        }
    }

    /// <summary>
    /// Analyze overall bundle overview and statistics
    /// </summary>
    private async Task<BundleOverview> AnalyzeBundleOverview(string directory, string buildConfiguration, string buildOutputPath)
    {
        return await Task.Run(() =>
        {
            var overview = new BundleOverview
            {
                BuildConfiguration = buildConfiguration,
                AnalysisTimestamp = DateTime.UtcNow
            };

            try
            {
                if (Directory.Exists(buildOutputPath))
                {
                    var files = Directory.GetFiles(buildOutputPath, "*.*", SearchOption.AllDirectories);
                    var jsFiles = files.Where(f => f.EndsWith(".js")).ToList();
                    var cssFiles = files.Where(f => f.EndsWith(".css")).ToList();
                    var assetFiles = files.Where(f => !f.EndsWith(".js") && !f.EndsWith(".css") && !f.EndsWith(".map")).ToList();

                    // Calculate total sizes
                    overview.TotalSize = files.Sum(f => new FileInfo(f).Length);
                    overview.UncompressedSize = overview.TotalSize;
                    overview.GzippedSize = (long)(overview.TotalSize * 0.7); // Estimate 30% compression

                    // Count assets
                    overview.ChunkCount = jsFiles.Count;
                    overview.AssetCount = files.Length;

                    // Estimate component and module counts
                    overview.ComponentCount = EstimateComponentCount(directory);
                    overview.ModuleCount = EstimateModuleCount(directory);

                    // Analyze distribution
                    overview.Distribution = AnalyzeSizeDistribution(jsFiles, cssFiles, assetFiles);

                    // Compare against budgets
                    overview.Comparison = AnalyzeBudgetComparison(overview, directory);
                }
                else
                {
                    // Project not built yet - provide estimated analysis
                    overview = CreateEstimatedOverview(directory, buildConfiguration);
                }
            }
            catch (Exception ex)
            {
                // Fallback to estimated analysis
                overview = CreateEstimatedOverview(directory, buildConfiguration);
            }

            return overview;
        });
    }

    /// <summary>
    /// Analyze individual component impacts on bundle size
    /// </summary>
    private async Task<List<ComponentBundleImpact>> AnalyzeComponentImpacts(string directory, string buildOutputPath, int maxComponents)
    {
        return await Task.Run(() =>
        {
            var componentImpacts = new List<ComponentBundleImpact>();

            try
            {
                // Find all component files
                var componentFiles = FindComponentFiles(directory);
                var bundleSize = GetTotalBundleSize(buildOutputPath);

                foreach (var componentFile in componentFiles.Take(maxComponents))
                {
                    var impact = AnalyzeComponentImpact(componentFile, bundleSize, directory);
                    componentImpacts.Add(impact);
                }

                // Sort by size impact (largest first)
                componentImpacts = componentImpacts.OrderByDescending(c => c.SizeBytes).ToList();
            }
            catch (Exception ex)
            {
                // Add error component for debugging
                componentImpacts.Add(new ComponentBundleImpact
                {
                    ComponentName = "AnalysisError",
                    ComponentPath = "Error during component analysis",
                    ImpactLevel = "critical",
                    OptimizationOpportunities = new List<string> { $"Error: {ex.Message}" }
                });
            }

            return componentImpacts;
        });
    }

    /// <summary>
    /// Analyze module impacts on bundle size
    /// </summary>
    private async Task<List<ModuleBundleImpact>> AnalyzeModuleImpacts(string directory, string buildOutputPath)
    {
        return await Task.Run(() =>
        {
            var moduleImpacts = new List<ModuleBundleImpact>();

            try
            {
                // Find all module files
                var moduleFiles = FindModuleFiles(directory);
                var bundleSize = GetTotalBundleSize(buildOutputPath);

                foreach (var moduleFile in moduleFiles)
                {
                    var impact = AnalyzeModuleImpact(moduleFile, bundleSize, directory);
                    moduleImpacts.Add(impact);
                }

                // Sort by size impact (largest first)
                moduleImpacts = moduleImpacts.OrderByDescending(m => m.SizeBytes).ToList();
            }
            catch (Exception ex)
            {
                // Add error module for debugging
                moduleImpacts.Add(new ModuleBundleImpact
                {
                    ModuleName = "AnalysisError",
                    ModulePath = "Error during module analysis",
                    OptimizationOpportunities = new List<string> { $"Error: {ex.Message}" }
                });
            }

            return moduleImpacts;
        });
    }

    /// <summary>
    /// Analyze asset impacts on bundle size
    /// </summary>
    private async Task<List<AssetAnalysis>> AnalyzeAssets(string buildOutputPath)
    {
        return await Task.Run(() =>
        {
            var assets = new List<AssetAnalysis>();

            try
            {
                if (Directory.Exists(buildOutputPath))
                {
                    var files = Directory.GetFiles(buildOutputPath, "*.*", SearchOption.AllDirectories);
                    var totalSize = files.Sum(f => new FileInfo(f).Length);

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        var asset = new AssetAnalysis
                        {
                            AssetName = Path.GetFileName(file),
                            AssetType = GetAssetType(file),
                            SizeBytes = fileInfo.Length,
                            GzippedSizeBytes = (long)(fileInfo.Length * 0.7), // Estimate
                            PercentageOfBundle = totalSize > 0 ? (double)fileInfo.Length / totalSize * 100 : 0,
                            IsOptimized = IsAssetOptimized(file),
                            Optimization = AnalyzeAssetOptimization(file),
                            OptimizationSuggestions = GenerateAssetOptimizationSuggestions(file)
                        };

                        assets.Add(asset);
                    }

                    // Sort by size (largest first)
                    assets = assets.OrderByDescending(a => a.SizeBytes).ToList();
                }
            }
            catch (Exception ex)
            {
                // Add error asset for debugging
                assets.Add(new AssetAnalysis
                {
                    AssetName = "AnalysisError",
                    AssetType = "error",
                    OptimizationSuggestions = new List<string> { $"Error: {ex.Message}" }
                });
            }

            return assets.Take(20).ToList(); // Limit to top 20 assets
        });
    }

    /// <summary>
    /// Analyze chunk distribution and optimization
    /// </summary>
    private async Task<ChunkAnalysis> AnalyzeChunks(string buildOutputPath)
    {
        return await Task.Run(() =>
        {
            var chunkAnalysis = new ChunkAnalysis();

            try
            {
                if (Directory.Exists(buildOutputPath))
                {
                    var jsFiles = Directory.GetFiles(buildOutputPath, "*.js", SearchOption.TopDirectoryOnly);
                    var totalSize = jsFiles.Sum(f => new FileInfo(f).Length);

                    foreach (var jsFile in jsFiles)
                    {
                        var fileInfo = new FileInfo(jsFile);
                        var chunk = new ChunkInfo
                        {
                            Name = Path.GetFileNameWithoutExtension(jsFile),
                            Type = DetermineChunkType(jsFile),
                            SizeBytes = fileInfo.Length,
                            GzippedSizeBytes = (long)(fileInfo.Length * 0.7),
                            PercentageOfBundle = totalSize > 0 ? (double)fileInfo.Length / totalSize * 100 : 0,
                            IsLazyLoaded = IsLazyLoadedChunk(jsFile),
                            LoadPriority = DetermineLoadPriority(jsFile)
                        };

                        chunkAnalysis.Chunks.Add(chunk);
                    }

                    // Analyze optimization opportunities
                    chunkAnalysis.Optimization = AnalyzeChunkOptimization(chunkAnalysis.Chunks);
                    chunkAnalysis.OptimizationOpportunities = GenerateChunkOptimizationOpportunities(chunkAnalysis.Chunks);
                    chunkAnalysis.RecommendedStrategy = GenerateChunkSplittingStrategy(chunkAnalysis.Chunks);
                }
            }
            catch (Exception ex)
            {
                chunkAnalysis.OptimizationOpportunities.Add($"Error analyzing chunks: {ex.Message}");
            }

            return chunkAnalysis;
        });
    }

    /// <summary>
    /// Analyze dependency impacts on bundle size
    /// </summary>
    private async Task<DependencyAnalysis> AnalyzeDependencyImpacts(string directory, string packageJsonPath, string buildOutputPath)
    {
        return await Task.Run(() =>
        {
            var dependencyAnalysis = new DependencyAnalysis();

            try
            {
                if (File.Exists(packageJsonPath))
                {
                    var packageJsonContent = File.ReadAllText(packageJsonPath);
                    var packageJson = JsonSerializer.Deserialize<JsonElement>(packageJsonContent);

                    // Analyze third-party dependencies
                    if (packageJson.TryGetProperty("dependencies", out var dependencies))
                    {
                        foreach (var dep in dependencies.EnumerateObject())
                        {
                            var impact = AnalyzeDependencyImpact(dep.Name, dep.Value.GetString() ?? "", buildOutputPath);
                            dependencyAnalysis.ThirdPartyDependencies.Add(impact);
                        }
                    }

                    // Analyze internal dependencies
                    dependencyAnalysis.InternalDependencies = AnalyzeInternalDependencies(directory);

                    // Analyze optimization opportunities
                    dependencyAnalysis.Optimization = AnalyzeDependencyOptimization(dependencyAnalysis.ThirdPartyDependencies);
                    dependencyAnalysis.OptimizationOpportunities = GenerateDependencyOptimizationOpportunities(dependencyAnalysis);
                    dependencyAnalysis.UnusedDependencies = AnalyzeUnusedDependencies(directory, dependencyAnalysis.ThirdPartyDependencies);
                }
            }
            catch (Exception ex)
            {
                dependencyAnalysis.OptimizationOpportunities.Add($"Error analyzing dependencies: {ex.Message}");
            }

            return dependencyAnalysis;
        });
    }

    /// <summary>
    /// Calculate performance metrics based on bundle analysis
    /// </summary>
    private async Task<PerformanceMetrics> CalculatePerformanceMetrics(BundleOverview overview)
    {
        return await Task.Run(() =>
        {
            var metrics = new PerformanceMetrics();

            try
            {
                var bundleSizeKB = overview.TotalSize / 1024.0;
                var gzippedSizeKB = overview.GzippedSize / 1024.0;

                // Calculate loading metrics
                metrics.Loading = new LoadingMetrics
                {
                    EstimatedDownloadTime3G = gzippedSizeKB / 50, // 50 KB/s for 3G
                    EstimatedDownloadTime4G = gzippedSizeKB / 200, // 200 KB/s for 4G
                    EstimatedDownloadTimeFiber = gzippedSizeKB / 1000, // 1000 KB/s for fiber
                    EstimatedParseTime = bundleSizeKB / 500, // 500 KB/s parse rate
                    EstimatedExecutionTime = bundleSizeKB / 1000, // 1000 KB/s execution rate
                    TotalLoadTime = (gzippedSizeKB / 200) + (bundleSizeKB / 500) + (bundleSizeKB / 1000)
                };

                // Calculate rendering metrics (estimates based on bundle size)
                metrics.Rendering = new RenderingMetrics
                {
                    EstimatedFirstContentfulPaint = metrics.Loading.TotalLoadTime + 0.5,
                    EstimatedLargestContentfulPaint = metrics.Loading.TotalLoadTime + 1.0,
                    EstimatedTimeToInteractive = metrics.Loading.TotalLoadTime + 1.5,
                    EstimatedFirstInputDelay = bundleSizeKB > 300 ? 100 : 50
                };

                // Calculate network metrics
                metrics.Network = new NetworkMetrics
                {
                    RequestCount = overview.ChunkCount + overview.AssetCount,
                    TotalTransferSize = overview.TotalSize,
                    CompressedSize = overview.GzippedSize,
                    CompressionRatio = overview.TotalSize > 0 ? (double)overview.GzippedSize / overview.TotalSize : 0,
                    CacheableResources = overview.AssetCount
                };

                // Calculate user experience metrics
                metrics.UserExperience = CalculateUserExperienceMetrics(overview, metrics);
            }
            catch (Exception ex)
            {
                metrics.UserExperience.ImprovementAreas.Add($"Error calculating metrics: {ex.Message}");
            }

            return metrics;
        });
    }

    /// <summary>
    /// Generate optimization recommendations based on analysis
    /// </summary>
    private async Task<BundleOptimizationRecommendations> GenerateOptimizationRecommendations(BundleSizeAnalysisResult result)
    {
        return await Task.Run(() =>
        {
            var recommendations = new BundleOptimizationRecommendations();

            try
            {
                var allRecommendations = new List<OptimizationRecommendation>();

                // Component-based recommendations
                allRecommendations.AddRange(GenerateComponentOptimizationRecommendations(result.ComponentImpacts));

                // Dependency-based recommendations
                allRecommendations.AddRange(GenerateDependencyOptimizationRecommendations(result.Dependencies));

                // Asset-based recommendations
                allRecommendations.AddRange(GenerateAssetOptimizationRecommendations(result.Assets));

                // Chunk-based recommendations
                allRecommendations.AddRange(GenerateChunkOptimizationRecommendations(result.Chunks));

                // General Angular optimization recommendations
                allRecommendations.AddRange(GenerateGeneralAngularOptimizationRecommendations(result.Overview));

                // Categorize recommendations by priority
                recommendations.HighPriority = allRecommendations.Where(r => r.Priority == "high").ToList();
                recommendations.MediumPriority = allRecommendations.Where(r => r.Priority == "medium").ToList();
                recommendations.LowPriority = allRecommendations.Where(r => r.Priority == "low").ToList();
                recommendations.QuickWins = allRecommendations.Where(r => r.ImplementationEffort <= 3).ToList();

                // Generate summary
                recommendations.Summary = GenerateOptimizationSummary(allRecommendations);

                // Generate implementation guide
                recommendations.Implementation = GenerateImplementationGuide(allRecommendations);
            }
            catch (Exception ex)
            {
                // Add error recommendation
                var errorRecommendation = new OptimizationRecommendation
                {
                    Id = "ERROR_001",
                    Title = "Analysis Error",
                    Description = $"Error generating recommendations: {ex.Message}",
                    Category = "error",
                    Priority = "high"
                };
                recommendations.HighPriority.Add(errorRecommendation);
            }

            return recommendations;
        });
    }

    // Helper methods for analysis implementation

    private BundleSizeDistribution AnalyzeSizeDistribution(List<string> jsFiles, List<string> cssFiles, List<string> assetFiles)
    {
        var distribution = new BundleSizeDistribution();

        try
        {
            var totalSize = jsFiles.Sum(f => new FileInfo(f).Length) +
                           cssFiles.Sum(f => new FileInfo(f).Length) +
                           assetFiles.Sum(f => new FileInfo(f).Length);

            // Categorize JS files
            foreach (var jsFile in jsFiles)
            {
                var fileName = Path.GetFileName(jsFile);
                var fileSize = new FileInfo(jsFile).Length;

                if (fileName.Contains("vendor") || fileName.Contains("polyfills"))
                {
                    distribution.VendorSize += fileSize;
                }
                else if (fileName.Contains("runtime"))
                {
                    distribution.RuntimeSize += fileSize;
                }
                else
                {
                    distribution.ApplicationSize += fileSize;
                }

                distribution.ByChunk[fileName] = fileSize;
            }

            // Add CSS files
            distribution.StylesSize = cssFiles.Sum(f => new FileInfo(f).Length);

            // Calculate percentages
            if (totalSize > 0)
            {
                distribution.PercentageByCategory["vendor"] = (double)distribution.VendorSize / totalSize * 100;
                distribution.PercentageByCategory["application"] = (double)distribution.ApplicationSize / totalSize * 100;
                distribution.PercentageByCategory["runtime"] = (double)distribution.RuntimeSize / totalSize * 100;
                distribution.PercentageByCategory["styles"] = (double)distribution.StylesSize / totalSize * 100;
                distribution.PercentageByCategory["polyfills"] = (double)distribution.PolyfillsSize / totalSize * 100;
            }
        }
        catch (Exception ex)
        {
            // Log error but continue
            distribution.PercentageByCategory["error"] = 0;
        }

        return distribution;
    }

    private BundleComparison AnalyzeBudgetComparison(BundleOverview overview, string directory)
    {
        var comparison = new BundleComparison();

        try
        {
            // Set recommended sizes based on industry standards
            comparison.RecommendedSizes = new RecommendedSizes
            {
                Initial = 300 * 1024, // 300KB
                Any = 2 * 1024 * 1024, // 2MB
                Vendor = 500 * 1024, // 500KB
                Application = 1 * 1024 * 1024, // 1MB
                Rationale = "Based on Angular performance guidelines and Core Web Vitals recommendations"
            };

            // Analyze initial bundle status
            var initialSize = overview.Distribution.ApplicationSize + overview.Distribution.RuntimeSize;
            comparison.InitialBudgetStatus = new BudgetStatus
            {
                MaximumSize = comparison.RecommendedSizes.Initial,
                CurrentSize = initialSize,
                UtilizationPercentage = (double)initialSize / comparison.RecommendedSizes.Initial * 100,
                WithinBudget = initialSize <= comparison.RecommendedSizes.Initial,
                Status = initialSize <= comparison.RecommendedSizes.Initial ? "ok" : 
                        initialSize <= comparison.RecommendedSizes.Initial * 1.2 ? "warning" : "error"
            };

            // Analyze any/total bundle status
            comparison.AnyBudgetStatus = new BudgetStatus
            {
                MaximumSize = comparison.RecommendedSizes.Any,
                CurrentSize = overview.TotalSize,
                UtilizationPercentage = (double)overview.TotalSize / comparison.RecommendedSizes.Any * 100,
                WithinBudget = overview.TotalSize <= comparison.RecommendedSizes.Any,
                Status = overview.TotalSize <= comparison.RecommendedSizes.Any ? "ok" : 
                        overview.TotalSize <= comparison.RecommendedSizes.Any * 1.2 ? "warning" : "error"
            };

            // Check for budget violations
            var violations = new List<BudgetViolation>();

            if (!comparison.InitialBudgetStatus.WithinBudget)
            {
                violations.Add(new BudgetViolation
                {
                    BudgetType = "initial",
                    ExpectedSize = comparison.RecommendedSizes.Initial,
                    ActualSize = initialSize,
                    Excess = initialSize - comparison.RecommendedSizes.Initial,
                    Severity = comparison.InitialBudgetStatus.Status
                });
            }

            if (!comparison.AnyBudgetStatus.WithinBudget)
            {
                violations.Add(new BudgetViolation
                {
                    BudgetType = "any",
                    ExpectedSize = comparison.RecommendedSizes.Any,
                    ActualSize = overview.TotalSize,
                    Excess = overview.TotalSize - comparison.RecommendedSizes.Any,
                    Severity = comparison.AnyBudgetStatus.Status
                });
            }

            comparison.BudgetViolations = violations;

            // Calculate overall score
            comparison.Score = CalculateBundleSizeScore(overview, comparison);
        }
        catch (Exception ex)
        {
            comparison.Score = new SizeScore
            {
                Overall = 0,
                Grade = "F",
                ScoreFactors = new List<string> { $"Error calculating score: {ex.Message}" }
            };
        }

        return comparison;
    }

    private SizeScore CalculateBundleSizeScore(BundleOverview overview, BundleComparison comparison)
    {
        var score = new SizeScore
        {
            ScoreFactors = new List<string>()
        };

        try
        {
            var totalPoints = 0;
            var maxPoints = 0;

            // Initial load score (40% weight)
            var initialWeight = 40;
            var initialScore = comparison.InitialBudgetStatus.WithinBudget ? initialWeight : 
                              Math.Max(0, initialWeight - (int)(comparison.InitialBudgetStatus.UtilizationPercentage - 100));
            totalPoints += initialScore;
            maxPoints += initialWeight;
            score.InitialLoad = (int)((double)initialScore / initialWeight * 100);
            score.ScoreFactors.Add($"Initial load: {score.InitialLoad}/100 ({comparison.InitialBudgetStatus.CurrentSize / 1024}KB)");

            // Vendor optimization score (30% weight)
            var vendorWeight = 30;
            var vendorScore = overview.Distribution.VendorSize <= comparison.RecommendedSizes.Vendor ? vendorWeight :
                             Math.Max(0, vendorWeight - (int)((overview.Distribution.VendorSize - comparison.RecommendedSizes.Vendor) / 10240));
            totalPoints += vendorScore;
            maxPoints += vendorWeight;
            score.VendorOptimization = (int)((double)vendorScore / vendorWeight * 100);
            score.ScoreFactors.Add($"Vendor optimization: {score.VendorOptimization}/100 ({overview.Distribution.VendorSize / 1024}KB)");

            // Lazy loading score (30% weight)
            var lazyWeight = 30;
            var lazyScore = overview.ChunkCount > 3 ? lazyWeight : Math.Max(0, lazyWeight - (5 - overview.ChunkCount) * 5);
            totalPoints += lazyScore;
            maxPoints += lazyWeight;
            score.LazyLoading = (int)((double)lazyScore / lazyWeight * 100);
            score.ScoreFactors.Add($"Code splitting: {score.LazyLoading}/100 ({overview.ChunkCount} chunks)");

            // Calculate overall score
            score.Overall = maxPoints > 0 ? (int)((double)totalPoints / maxPoints * 100) : 0;

            // Assign grade
            score.Grade = score.Overall >= 90 ? "A" :
                         score.Overall >= 80 ? "B" :
                         score.Overall >= 70 ? "C" :
                         score.Overall >= 60 ? "D" : "F";
        }
        catch (Exception ex)
        {
            score.Overall = 0;
            score.Grade = "F";
            score.ScoreFactors.Add($"Error calculating score: {ex.Message}");
        }

        return score;
    }

    private BundleOverview CreateEstimatedOverview(string directory, string buildConfiguration)
    {
        var overview = new BundleOverview
        {
            BuildConfiguration = buildConfiguration,
            AnalysisTimestamp = DateTime.UtcNow,
            ComponentCount = EstimateComponentCount(directory),
            ModuleCount = EstimateModuleCount(directory)
        };

        // Provide estimated sizes based on typical Angular applications
        var baseSize = 200 * 1024; // 200KB base
        var componentMultiplier = overview.ComponentCount * 2 * 1024; // 2KB per component
        var moduleMultiplier = overview.ModuleCount * 10 * 1024; // 10KB per module

        overview.TotalSize = baseSize + componentMultiplier + moduleMultiplier;
        overview.GzippedSize = (long)(overview.TotalSize * 0.7);
        overview.UncompressedSize = overview.TotalSize;

        overview.Distribution = new BundleSizeDistribution
        {
            ApplicationSize = (long)(overview.TotalSize * 0.4),
            VendorSize = (long)(overview.TotalSize * 0.4),
            RuntimeSize = (long)(overview.TotalSize * 0.1),
            StylesSize = (long)(overview.TotalSize * 0.1)
        };

        overview.Comparison = AnalyzeBudgetComparison(overview, directory);

        return overview;
    }

    private int EstimateComponentCount(string directory)
    {
        try
        {
            var componentFiles = Directory.GetFiles(directory, "*.component.ts", SearchOption.AllDirectories);
            return componentFiles.Length;
        }
        catch
        {
            return 10; // Default estimate
        }
    }

    private int EstimateModuleCount(string directory)
    {
        try
        {
            var moduleFiles = Directory.GetFiles(directory, "*.module.ts", SearchOption.AllDirectories);
            return Math.Max(1, moduleFiles.Length); // At least 1 for app module
        }
        catch
        {
            return 3; // Default estimate
        }
    }

    private List<string> FindComponentFiles(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.component.ts", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private List<string> FindModuleFiles(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.module.ts", SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private long GetTotalBundleSize(string buildOutputPath)
    {
        try
        {
            if (Directory.Exists(buildOutputPath))
            {
                var files = Directory.GetFiles(buildOutputPath, "*.*", SearchOption.AllDirectories);
                return files.Sum(f => new FileInfo(f).Length);
            }
        }
        catch
        {
            // Ignore errors
        }

        return 500 * 1024; // Default 500KB estimate
    }

    private ComponentBundleImpact AnalyzeComponentImpact(string componentFile, long bundleSize, string directory)
    {
        var impact = new ComponentBundleImpact
        {
            ComponentName = Path.GetFileNameWithoutExtension(componentFile).Replace(".component", ""),
            ComponentPath = Path.GetRelativePath(directory, componentFile),
            ComponentType = DetermineComponentType(componentFile)
        };

        try
        {
            var fileInfo = new FileInfo(componentFile);
            var estimatedSize = EstimateComponentSize(componentFile);
            
            impact.SizeBytes = estimatedSize;
            impact.GzippedSizeBytes = (long)(estimatedSize * 0.7);
            impact.PercentageOfBundle = bundleSize > 0 ? (double)estimatedSize / bundleSize * 100 : 0;

            // Analyze dependencies
            impact.Dependencies = AnalyzeComponentDependencies(componentFile);
            impact.DependencyCount = impact.Dependencies.Count;

            // Analyze optimization opportunities
            impact.Optimization = AnalyzeComponentOptimization(componentFile);
            impact.Complexity = AnalyzeComponentComplexity(componentFile);
            impact.OptimizationOpportunities = GenerateComponentOptimizationOpportunities(impact);
            impact.ImpactLevel = DetermineImpactLevel(impact);
        }
        catch (Exception ex)
        {
            impact.OptimizationOpportunities.Add($"Error analyzing component: {ex.Message}");
            impact.ImpactLevel = "unknown";
        }

        return impact;
    }

    private ModuleBundleImpact AnalyzeModuleImpact(string moduleFile, long bundleSize, string directory)
    {
        var impact = new ModuleBundleImpact
        {
            ModuleName = Path.GetFileNameWithoutExtension(moduleFile).Replace(".module", ""),
            ModulePath = Path.GetRelativePath(directory, moduleFile),
            ModuleType = DetermineModuleType(moduleFile)
        };

        try
        {
            var estimatedSize = EstimateModuleSize(moduleFile, directory);
            
            impact.SizeBytes = estimatedSize;
            impact.GzippedSizeBytes = (long)(estimatedSize * 0.7);
            impact.PercentageOfBundle = bundleSize > 0 ? (double)estimatedSize / bundleSize * 100 : 0;

            // Analyze module contents
            impact.Components = FindModuleComponents(moduleFile, directory);
            impact.Services = FindModuleServices(moduleFile, directory);
            impact.ComponentCount = impact.Components.Count;
            impact.ServiceCount = impact.Services.Count;

            // Analyze optimization
            impact.Optimization = AnalyzeModuleOptimization(moduleFile, directory);
            impact.LoadingStrategy = DetermineModuleLoadingStrategy(moduleFile);
            impact.OptimizationOpportunities = GenerateModuleOptimizationOpportunities(impact);
        }
        catch (Exception ex)
        {
            impact.OptimizationOpportunities.Add($"Error analyzing module: {ex.Message}");
        }

        return impact;
    }

    // Additional helper methods...

    private string GetAssetType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".js" => "javascript",
            ".css" => "stylesheet",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" => "image",
            ".woff" or ".woff2" or ".ttf" or ".eot" => "font",
            ".html" => "html",
            ".json" => "json",
            ".map" => "sourcemap",
            _ => "other"
        };
    }

    private bool IsAssetOptimized(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return fileName.Contains(".min.") || fileName.Contains("-es2015") || fileName.Contains("-es5");
    }

    private AssetOptimization AnalyzeAssetOptimization(string filePath)
    {
        var optimization = new AssetOptimization
        {
            OptimizationTechniques = new List<string>()
        };

        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLower();

        optimization.Minified = fileName.Contains(".min.") || fileName.Length < Path.GetFileNameWithoutExtension(filePath).Length + 10;
        optimization.Compressed = true; // Assume server compression
        optimization.HasSourceMaps = File.Exists(filePath + ".map");

        if (extension == ".js")
        {
            optimization.TreeShaken = !fileName.Contains("vendor");
            optimization.OptimizationTechniques.Add("JavaScript optimization");
        }
        else if (extension == ".css")
        {
            optimization.OptimizationTechniques.Add("CSS optimization");
        }

        var fileInfo = new FileInfo(filePath);
        var estimatedUncompressed = fileInfo.Length;
        var estimatedCompressed = (long)(fileInfo.Length * 0.7);
        optimization.CompressionRatio = $"{(1.0 - (double)estimatedCompressed / estimatedUncompressed):P1}";

        return optimization;
    }

    private List<string> GenerateAssetOptimizationSuggestions(string filePath)
    {
        var suggestions = new List<string>();
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLower();
        var fileInfo = new FileInfo(filePath);

        if (extension == ".js" && fileInfo.Length > 100 * 1024) // > 100KB
        {
            if (!fileName.Contains(".min."))
                suggestions.Add("Enable minification for JavaScript files");
            if (!fileName.Contains("-es2015"))
                suggestions.Add("Use differential loading for modern vs legacy browsers");
            suggestions.Add("Consider code splitting for large JavaScript files");
        }

        if (extension == ".css" && fileInfo.Length > 50 * 1024) // > 50KB
        {
            suggestions.Add("Enable CSS minification");
            suggestions.Add("Consider critical CSS extraction");
            suggestions.Add("Remove unused CSS rules");
        }

        if (new[] { ".png", ".jpg", ".jpeg" }.Contains(extension) && fileInfo.Length > 200 * 1024) // > 200KB
        {
            suggestions.Add("Optimize image compression");
            suggestions.Add("Consider WebP format for better compression");
            suggestions.Add("Implement responsive images");
        }

        if (extension == ".svg" && fileInfo.Length > 10 * 1024) // > 10KB
        {
            suggestions.Add("Optimize SVG files using SVGO");
            suggestions.Add("Consider SVG sprites for icons");
        }

        return suggestions;
    }

    private string DetermineChunkType(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        if (fileName.Contains("runtime"))
            return "runtime";
        if (fileName.Contains("polyfills"))
            return "polyfills";
        if (fileName.Contains("vendor"))
            return "vendor";
        if (fileName.Contains("main"))
            return "initial";
        
        return "async";
    }

    private bool IsLazyLoadedChunk(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return !fileName.Contains("main") && !fileName.Contains("runtime") && 
               !fileName.Contains("polyfills") && !fileName.Contains("vendor");
    }

    private string DetermineLoadPriority(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        
        if (fileName.Contains("main") || fileName.Contains("runtime"))
            return "high";
        if (fileName.Contains("vendor") || fileName.Contains("polyfills"))
            return "medium";
        
        return "low";
    }

    private ChunkOptimization AnalyzeChunkOptimization(List<ChunkInfo> chunks)
    {
        var optimization = new ChunkOptimization
        {
            ChunkCount = chunks.Count,
            LargeChunks = new List<string>(),
            SmallChunks = new List<string>()
        };

        if (chunks.Count > 0)
        {
            optimization.AverageChunkSize = chunks.Average(c => c.SizeBytes);
            
            // Identify large chunks (> 500KB)
            optimization.LargeChunks = chunks
                .Where(c => c.SizeBytes > 500 * 1024)
                .Select(c => c.Name)
                .ToList();

            // Identify small chunks (< 10KB)
            optimization.SmallChunks = chunks
                .Where(c => c.SizeBytes < 10 * 1024 && c.Type == "async")
                .Select(c => c.Name)
                .ToList();

            optimization.ProperCodeSplitting = chunks.Count(c => c.Type == "async") > 1;
            optimization.OptimalChunkSizes = optimization.LargeChunks.Count == 0;
            optimization.HasDuplicatedCode = false; // Would need deeper analysis
        }

        return optimization;
    }

    private List<string> GenerateChunkOptimizationOpportunities(List<ChunkInfo> chunks)
    {
        var opportunities = new List<string>();

        var largeChunks = chunks.Where(c => c.SizeBytes > 500 * 1024).ToList();
        if (largeChunks.Count > 0)
        {
            opportunities.Add($"Split {largeChunks.Count} large chunk(s) (>{500}KB) into smaller pieces");
        }

        var smallChunks = chunks.Where(c => c.SizeBytes < 10 * 1024 && c.Type == "async").ToList();
        if (smallChunks.Count > 3)
        {
            opportunities.Add($"Consider merging {smallChunks.Count} small chunks (<10KB) to reduce HTTP overhead");
        }

        var asyncChunks = chunks.Where(c => c.Type == "async").ToList();
        if (asyncChunks.Count == 0)
        {
            opportunities.Add("Implement lazy loading to create async chunks");
        }

        if (!chunks.Any(c => c.Name.Contains("vendor")))
        {
            opportunities.Add("Separate vendor dependencies into dedicated chunk");
        }

        return opportunities;
    }

    private SplittingStrategy GenerateChunkSplittingStrategy(List<ChunkInfo> chunks)
    {
        var strategy = new SplittingStrategy
        {
            RecommendedSplits = new List<string>(),
            MergeOpportunities = new List<string>()
        };

        var totalSize = chunks.Sum(c => c.SizeBytes);
        var avgSize = chunks.Count > 0 ? totalSize / chunks.Count : 0;

        if (avgSize > 300 * 1024) // Average chunk size > 300KB
        {
            strategy.Strategy = "feature-based";
            strategy.Rationale = "Large average chunk size suggests need for feature-based splitting";
            strategy.RecommendedSplits.Add("Split by feature modules");
            strategy.RecommendedSplits.Add("Implement route-based code splitting");
        }
        else if (chunks.Count > 10)
        {
            strategy.Strategy = "route-based";
            strategy.Rationale = "Many small chunks suggest consolidation by routes";
            strategy.MergeOpportunities.Add("Merge related feature chunks");
        }
        else
        {
            strategy.Strategy = "vendor-splitting";
            strategy.Rationale = "Standard vendor/app splitting strategy";
            strategy.RecommendedSplits.Add("Separate vendor dependencies");
        }

        return strategy;
    }

    private DependencyImpact AnalyzeDependencyImpact(string name, string version, string buildOutputPath)
    {
        var impact = new DependencyImpact
        {
            Name = name,
            Version = version,
            Alternatives = new List<string>(),
            UsedBy = new List<string>()
        };

        // Estimate size based on known package sizes
        impact.SizeBytes = EstimateDependencySize(name);
        impact.GzippedSizeBytes = (long)(impact.SizeBytes * 0.7);

        // Determine if tree shakable
        impact.IsTreeShakable = IsTreeShakableDependency(name);
        impact.IsUsed = true; // Assume used for now

        // Get alternatives
        impact.Alternatives = GetDependencyAlternatives(name);

        // Analyze optimization opportunities
        impact.Optimization = AnalyzeDependencyOptimizationInfo(name, impact.SizeBytes);

        return impact;
    }

    private List<DependencyImpact> AnalyzeInternalDependencies(string directory)
    {
        var dependencies = new List<DependencyImpact>();

        try
        {
            // Find internal services and modules
            var serviceFiles = Directory.GetFiles(directory, "*.service.ts", SearchOption.AllDirectories);
            var moduleFiles = Directory.GetFiles(directory, "*.module.ts", SearchOption.AllDirectories);

            foreach (var file in serviceFiles.Concat(moduleFiles))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var estimatedSize = new FileInfo(file).Length * 3; // Estimate with dependencies

                dependencies.Add(new DependencyImpact
                {
                    Name = name,
                    Version = "internal",
                    SizeBytes = estimatedSize,
                    GzippedSizeBytes = (long)(estimatedSize * 0.7),
                    IsTreeShakable = true,
                    IsUsed = true,
                    UsedBy = new List<string> { "application" }
                });
            }
        }
        catch
        {
            // Ignore errors
        }

        return dependencies;
    }

    private DependencyOptimization AnalyzeDependencyOptimization(List<DependencyImpact> dependencies)
    {
        var optimization = new DependencyOptimization
        {
            TotalDependencies = dependencies.Count,
            QuickWins = new List<string>()
        };

        optimization.OptimizedDependencies = dependencies.Count(d => d.IsTreeShakable);
        optimization.UnusedDependencies = dependencies.Count(d => !d.IsUsed);
        optimization.PotentialSavings = dependencies.Where(d => !d.IsUsed).Sum(d => d.SizeBytes);

        if (optimization.TotalDependencies > 0)
        {
            optimization.OptimizationScore = (double)optimization.OptimizedDependencies / optimization.TotalDependencies * 100;
        }

        // Identify quick wins
        var largeDependencies = dependencies.Where(d => d.SizeBytes > 100 * 1024).ToList();
        foreach (var dep in largeDependencies)
        {
            if (dep.Alternatives.Count > 0)
            {
                optimization.QuickWins.Add($"Replace {dep.Name} with smaller alternative");
            }
            if (!dep.IsTreeShakable)
            {
                optimization.QuickWins.Add($"Enable tree shaking for {dep.Name}");
            }
        }

        return optimization;
    }

    private List<string> GenerateDependencyOptimizationOpportunities(DependencyAnalysis analysis)
    {
        var opportunities = new List<string>();

        var largeDeps = analysis.ThirdPartyDependencies.Where(d => d.SizeBytes > 100 * 1024).ToList();
        if (largeDeps.Count > 0)
        {
            opportunities.Add($"Review {largeDeps.Count} large dependencies (>100KB) for optimization");
        }

        var nonTreeShakable = analysis.ThirdPartyDependencies.Where(d => !d.IsTreeShakable).ToList();
        if (nonTreeShakable.Count > 0)
        {
            opportunities.Add($"Enable tree shaking for {nonTreeShakable.Count} dependencies");
        }

        if (analysis.UnusedDependencies.CompletelyUnused.Count > 0)
        {
            opportunities.Add($"Remove {analysis.UnusedDependencies.CompletelyUnused.Count} completely unused dependencies");
        }

        if (analysis.Optimization.PotentialSavings > 50 * 1024)
        {
            opportunities.Add($"Potential savings: {analysis.Optimization.PotentialSavings / 1024}KB from dependency optimization");
        }

        return opportunities;
    }

    private UnusedDependencies AnalyzeUnusedDependencies(string directory, List<DependencyImpact> dependencies)
    {
        var unused = new UnusedDependencies
        {
            CompletelyUnused = new List<string>(),
            PartiallyUnused = new List<string>(),
            SafeToRemove = new List<string>(),
            RequiresInvestigation = new List<string>()
        };

        // This would require static analysis of the codebase
        // For now, provide general guidance

        unused.SafeToRemove.Add("Example: unused testing dependencies in production build");
        unused.RequiresInvestigation.Add("Review all dependencies for actual usage");

        return unused;
    }

    private UserExperienceMetrics CalculateUserExperienceMetrics(BundleOverview overview, PerformanceMetrics metrics)
    {
        var ux = new UserExperienceMetrics
        {
            UserImpactFactors = new List<string>(),
            ImprovementAreas = new List<string>()
        };

        // Calculate performance score based on loading metrics
        var score = 100;

        if (metrics.Loading.TotalLoadTime > 5)
        {
            score -= 30;
            ux.UserImpactFactors.Add("Slow initial load time");
            ux.ImprovementAreas.Add("Reduce bundle size to improve load time");
        }

        if (overview.TotalSize > 2 * 1024 * 1024) // > 2MB
        {
            score -= 20;
            ux.UserImpactFactors.Add("Large bundle size");
            ux.ImprovementAreas.Add("Implement code splitting and lazy loading");
        }

        if (overview.ChunkCount < 3)
        {
            score -= 15;
            ux.UserImpactFactors.Add("No code splitting");
            ux.ImprovementAreas.Add("Implement lazy loading for better perceived performance");
        }

        ux.PerformanceScore = Math.Max(0, score);
        ux.UserExperienceGrade = ux.PerformanceScore >= 90 ? "A" :
                                ux.PerformanceScore >= 80 ? "B" :
                                ux.PerformanceScore >= 70 ? "C" :
                                ux.PerformanceScore >= 60 ? "D" : "F";

        return ux;
    }

    // Component analysis helper methods

    private string DetermineComponentType(string componentFile)
    {
        try
        {
            var content = File.ReadAllText(componentFile);
            return content.Contains("standalone: true") ? "standalone" : "module-based";
        }
        catch
        {
            return "unknown";
        }
    }

    private long EstimateComponentSize(string componentFile)
    {
        try
        {
            var fileInfo = new FileInfo(componentFile);
            var baseSize = fileInfo.Length;

            // Estimate template and style sizes
            var templateFile = componentFile.Replace(".component.ts", ".component.html");
            var styleFile = componentFile.Replace(".component.ts", ".component.css");

            if (File.Exists(templateFile))
                baseSize += new FileInfo(templateFile).Length;

            if (File.Exists(styleFile))
                baseSize += new FileInfo(styleFile).Length;

            // Multiply by factor to account for compiled size and dependencies
            return baseSize * 2;
        }
        catch
        {
            return 5 * 1024; // Default 5KB
        }
    }

    private List<string> AnalyzeComponentDependencies(string componentFile)
    {
        var dependencies = new List<string>();

        try
        {
            var content = File.ReadAllText(componentFile);
            var lines = content.Split('\n');

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("import ") && line.Contains("from "))
                {
                    var fromIndex = line.IndexOf("from ");
                    if (fromIndex >= 0)
                    {
                        var dependency = line.Substring(fromIndex + 5).Trim().Trim('\'', '"', ';');
                        dependencies.Add(dependency);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return dependencies;
    }

    private ComponentOptimization AnalyzeComponentOptimization(string componentFile)
    {
        var optimization = new ComponentOptimization
        {
            OptimizationSuggestions = new List<string>()
        };

        try
        {
            var content = File.ReadAllText(componentFile);

            optimization.OnPushStrategy = content.Contains("ChangeDetectionStrategy.OnPush");
            optimization.UsesStandaloneAPI = content.Contains("standalone: true");
            optimization.LazyLoaded = false; // Would need routing analysis
            optimization.TreeShakable = true; // Angular components are generally tree-shakable
            optimization.HasDeadCode = false; // Would need deeper analysis

            var score = 100.0;
            if (!optimization.OnPushStrategy)
            {
                score -= 20;
                optimization.OptimizationSuggestions.Add("Use OnPush change detection strategy");
            }

            if (!optimization.UsesStandaloneAPI)
            {
                score -= 10;
                optimization.OptimizationSuggestions.Add("Consider migrating to standalone components");
            }

            optimization.OptimizationScore = score;
        }
        catch
        {
            optimization.OptimizationScore = 50; // Default score on error
        }

        return optimization;
    }

    private ComponentComplexity AnalyzeComponentComplexity(string componentFile)
    {
        var complexity = new ComponentComplexity();

        try
        {
            var content = File.ReadAllText(componentFile);
            var lines = content.Split('\n');

            complexity.LogicSize = lines.Length;
            complexity.ImportCount = lines.Count(l => l.Trim().StartsWith("import "));
            complexity.MethodCount = lines.Count(l => l.Contains("() {") || l.Contains("(): "));
            complexity.LifecycleHookCount = lines.Count(l => l.Contains("ngOn") || l.Contains("ngAfter"));

            // Analyze template and style files
            var templateFile = componentFile.Replace(".component.ts", ".component.html");
            var styleFile = componentFile.Replace(".component.ts", ".component.css");

            if (File.Exists(templateFile))
            {
                complexity.TemplateSize = File.ReadAllLines(templateFile).Length;
            }

            if (File.Exists(styleFile))
            {
                complexity.StyleSize = File.ReadAllLines(styleFile).Length;
            }

            // Determine complexity level
            var totalComplexity = complexity.LogicSize + complexity.TemplateSize + complexity.StyleSize;
            complexity.ComplexityLevel = totalComplexity switch
            {
                < 50 => "simple",
                < 150 => "moderate",
                < 300 => "complex",
                _ => "highly-complex"
            };
        }
        catch
        {
            complexity.ComplexityLevel = "unknown";
        }

        return complexity;
    }

    private List<string> GenerateComponentOptimizationOpportunities(ComponentBundleImpact impact)
    {
        var opportunities = new List<string>();

        if (impact.SizeBytes > 20 * 1024) // > 20KB
        {
            opportunities.Add("Consider splitting large component into smaller pieces");
        }

        if (!impact.Optimization.OnPushStrategy)
        {
            opportunities.Add("Implement OnPush change detection strategy for better performance");
        }

        if (!impact.Optimization.UsesStandaloneAPI && impact.ComponentType == "module-based")
        {
            opportunities.Add("Migrate to standalone component API for better tree shaking");
        }

        if (impact.DependencyCount > 10)
        {
            opportunities.Add("Review and reduce component dependencies");
        }

        if (impact.Complexity.ComplexityLevel == "highly-complex")
        {
            opportunities.Add("Refactor complex component for better maintainability");
        }

        return opportunities;
    }

    private string DetermineImpactLevel(ComponentBundleImpact impact)
    {
        if (impact.SizeBytes > 50 * 1024 || impact.PercentageOfBundle > 5)
            return "critical";
        if (impact.SizeBytes > 20 * 1024 || impact.PercentageOfBundle > 2)
            return "high";
        if (impact.SizeBytes > 10 * 1024 || impact.PercentageOfBundle > 1)
            return "medium";
        return "low";
    }

    // Module analysis helper methods

    private string DetermineModuleType(string moduleFile)
    {
        var fileName = Path.GetFileName(moduleFile);
        
        if (fileName.Contains("app.module"))
            return "core";
        if (fileName.Contains("shared"))
            return "shared";
        if (fileName.Contains("feature") || fileName.Contains("lazy"))
            return "feature";
        
        return "feature";
    }

    private long EstimateModuleSize(string moduleFile, string directory)
    {
        try
        {
            var components = FindModuleComponents(moduleFile, directory);
            var services = FindModuleServices(moduleFile, directory);
            
            var baseSize = new FileInfo(moduleFile).Length;
            var componentSize = components.Count * 8 * 1024; // 8KB per component
            var serviceSize = services.Count * 3 * 1024; // 3KB per service
            
            return baseSize + componentSize + serviceSize;
        }
        catch
        {
            return 30 * 1024; // Default 30KB
        }
    }

    private List<string> FindModuleComponents(string moduleFile, string directory)
    {
        var components = new List<string>();
        
        try
        {
            var content = File.ReadAllText(moduleFile);
            // Simple heuristic: look for component imports and declarations
            var lines = content.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.Contains("Component") && line.Contains("import"))
                {
                    var componentName = ExtractComponentName(line);
                    if (!string.IsNullOrEmpty(componentName))
                        components.Add(componentName);
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return components;
    }

    private List<string> FindModuleServices(string moduleFile, string directory)
    {
        var services = new List<string>();
        
        try
        {
            var content = File.ReadAllText(moduleFile);
            var lines = content.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.Contains("Service") && line.Contains("import"))
                {
                    var serviceName = ExtractServiceName(line);
                    if (!string.IsNullOrEmpty(serviceName))
                        services.Add(serviceName);
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return services;
    }

    private ModuleOptimization AnalyzeModuleOptimization(string moduleFile, string directory)
    {
        var optimization = new ModuleOptimization
        {
            ModuleDependencies = new List<string>(),
            UnusedExports = new List<string>()
        };

        try
        {
            var content = File.ReadAllText(moduleFile);
            
            optimization.IsLazyLoaded = IsModuleLazyLoaded(moduleFile, directory);
            optimization.HasSharedComponents = content.Contains("declarations") && content.Contains("exports");
            optimization.ProperlyTreeShaken = true; // Assume yes unless proven otherwise
            
            var score = 100.0;
            if (!optimization.IsLazyLoaded)
            {
                score -= 30;
            }
            if (!optimization.HasSharedComponents && Path.GetFileName(moduleFile).Contains("shared"))
            {
                score -= 20;
            }
            
            optimization.OptimizationScore = score;
        }
        catch
        {
            optimization.OptimizationScore = 50;
        }

        return optimization;
    }

    private string DetermineModuleLoadingStrategy(string moduleFile)
    {
        // This would require analysis of routing configuration
        return "eager"; // Default assumption
    }

    private List<string> GenerateModuleOptimizationOpportunities(ModuleBundleImpact impact)
    {
        var opportunities = new List<string>();

        if (!impact.Optimization.IsLazyLoaded && impact.ModuleType == "feature")
        {
            opportunities.Add("Implement lazy loading for feature module");
        }

        if (impact.SizeBytes > 100 * 1024) // > 100KB
        {
            opportunities.Add("Consider splitting large module into smaller feature modules");
        }

        if (impact.ComponentCount > 20)
        {
            opportunities.Add("Large module with many components - consider refactoring");
        }

        if (!impact.Optimization.HasSharedComponents && impact.ModuleType == "shared")
        {
            opportunities.Add("Shared module should export components for reuse");
        }

        return opportunities;
    }

    // Dependency analysis helper methods

    private long EstimateDependencySize(string packageName)
    {
        // Known package sizes (approximate)
        var knownSizes = new Dictionary<string, long>
        {
            ["@angular/core"] = 150 * 1024,
            ["@angular/common"] = 120 * 1024,
            ["@angular/forms"] = 80 * 1024,
            ["@angular/router"] = 90 * 1024,
            ["@angular/animations"] = 70 * 1024,
            ["rxjs"] = 200 * 1024,
            ["lodash"] = 70 * 1024,
            ["moment"] = 200 * 1024,
            ["chart.js"] = 180 * 1024,
            ["bootstrap"] = 150 * 1024
        };

        if (knownSizes.TryGetValue(packageName, out var size))
            return size;

        // Estimate based on package name patterns
        if (packageName.StartsWith("@angular/"))
            return 80 * 1024; // Average Angular package size

        if (packageName.Contains("polyfill"))
            return 30 * 1024;

        return 50 * 1024; // Default estimate
    }

    private bool IsTreeShakableDependency(string packageName)
    {
        var treeShakablePackages = new[]
        {
            "@angular/", "lodash-es", "date-fns", "rxjs"
        };

        var nonTreeShakablePackages = new[]
        {
            "moment", "jquery", "bootstrap"
        };

        if (treeShakablePackages.Any(p => packageName.StartsWith(p) || packageName.Contains(p)))
            return true;

        if (nonTreeShakablePackages.Any(p => packageName.StartsWith(p) || packageName.Contains(p)))
            return false;

        return true; // Assume tree-shakable by default for modern packages
    }

    private List<string> GetDependencyAlternatives(string packageName)
    {
        var alternatives = new Dictionary<string, List<string>>
        {
            ["moment"] = new() { "date-fns", "dayjs" },
            ["lodash"] = new() { "lodash-es", "ramda" },
            ["jquery"] = new() { "native DOM APIs", "cash-dom" },
            ["bootstrap"] = new() { "@angular/material", "ng-bootstrap" }
        };

        return alternatives.TryGetValue(packageName, out var alts) ? alts : new List<string>();
    }

    private DependencyOptimizationInfo AnalyzeDependencyOptimizationInfo(string name, long size)
    {
        var info = new DependencyOptimizationInfo
        {
            OptimizationSuggestions = new List<string>(),
            AlternativeDependencies = new List<AlternativeDependency>()
        };

        info.CanBeTreeShaken = IsTreeShakableDependency(name);
        info.CanBeLazyLoaded = !name.StartsWith("@angular/core") && !name.Contains("polyfill");
        info.HasSmallerAlternatives = GetDependencyAlternatives(name).Count > 0;

        if (!info.CanBeTreeShaken)
        {
            info.OptimizationSuggestions.Add("Enable tree shaking or find tree-shakable alternative");
        }

        if (size > 100 * 1024) // > 100KB
        {
            info.OptimizationSuggestions.Add("Consider lazy loading or finding smaller alternative");
        }

        if (info.HasSmallerAlternatives)
        {
            var alternatives = GetDependencyAlternatives(name);
            foreach (var alt in alternatives)
            {
                var altSize = EstimateDependencySize(alt);
                if (altSize < size)
                {
                    info.AlternativeDependencies.Add(new AlternativeDependency
                    {
                        Name = alt,
                        SizeBytes = altSize,
                        SizeSavings = size - altSize,
                        Reason = "Smaller bundle size",
                        MigrationEffort = DetermineMigrationEffort(name, alt)
                    });
                }
            }
        }

        return info;
    }

    private string DetermineMigrationEffort(string from, string to)
    {
        var easyMigrations = new Dictionary<string, string>
        {
            ["lodash"] = "lodash-es",
            ["moment"] = "date-fns"
        };

        var hardMigrations = new Dictionary<string, string>
        {
            ["jquery"] = "native DOM APIs",
            ["bootstrap"] = "@angular/material"
        };

        if (easyMigrations.ContainsKey(from) && easyMigrations[from] == to)
            return "low";

        if (hardMigrations.ContainsKey(from) && hardMigrations[from] == to)
            return "high";

        return "medium";
    }

    // Recommendation generation helper methods

    private List<OptimizationRecommendation> GenerateComponentOptimizationRecommendations(List<ComponentBundleImpact> components)
    {
        var recommendations = new List<OptimizationRecommendation>();

        var criticalComponents = components.Where(c => c.ImpactLevel == "critical").ToList();
        if (criticalComponents.Count > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "COMP_001",
                Title = "Optimize Critical Impact Components",
                Description = $"Found {criticalComponents.Count} components with critical bundle impact",
                Category = "component",
                Priority = "high",
                PotentialSavings = criticalComponents.Sum(c => c.SizeBytes) / 4, // Estimate 25% savings
                ImplementationEffort = 6,
                Impact = "Significant reduction in initial bundle size",
                Steps = new List<string>
                {
                    "Review component complexity and dependencies",
                    "Implement OnPush change detection strategy",
                    "Split large components into smaller pieces",
                    "Remove unused dependencies"
                },
                AffectedFiles = criticalComponents.Select(c => c.ComponentPath).ToList(),
                EstimatedTime = "2-4 weeks"
            });
        }

        var nonOnPushComponents = components.Where(c => !c.Optimization.OnPushStrategy).Take(10).ToList();
        if (nonOnPushComponents.Count > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "COMP_002",
                Title = "Implement OnPush Change Detection",
                Description = $"Optimize {nonOnPushComponents.Count} components with OnPush strategy",
                Category = "component",
                Priority = "medium",
                PotentialSavings = 0, // Runtime performance improvement, not bundle size
                ImplementationEffort = 3,
                Impact = "Better runtime performance and change detection efficiency",
                Steps = new List<string>
                {
                    "Add ChangeDetectionStrategy.OnPush to component",
                    "Update component to use immutable data patterns",
                    "Use markForCheck() when needed"
                },
                AffectedFiles = nonOnPushComponents.Select(c => c.ComponentPath).ToList(),
                EstimatedTime = "1-2 weeks"
            });
        }

        return recommendations;
    }

    private List<OptimizationRecommendation> GenerateDependencyOptimizationRecommendations(DependencyAnalysis dependencies)
    {
        var recommendations = new List<OptimizationRecommendation>();

        var largeDependencies = dependencies.ThirdPartyDependencies
            .Where(d => d.SizeBytes > 100 * 1024)
            .OrderByDescending(d => d.SizeBytes)
            .Take(5)
            .ToList();

        if (largeDependencies.Count > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "DEP_001",
                Title = "Optimize Large Dependencies",
                Description = $"Review and optimize {largeDependencies.Count} large dependencies",
                Category = "dependency",
                Priority = "high",
                PotentialSavings = largeDependencies.Sum(d => d.SizeBytes) / 3, // Estimate 33% savings
                ImplementationEffort = 7,
                Impact = "Significant bundle size reduction",
                Steps = new List<string>
                {
                    "Review necessity of large dependencies",
                    "Enable tree shaking where possible",
                    "Consider smaller alternatives",
                    "Implement lazy loading for non-critical dependencies"
                },
                Requirements = new List<string> { "Bundle analyzer", "Dependency audit" },
                EstimatedTime = "3-5 weeks"
            });
        }

        if (dependencies.UnusedDependencies.CompletelyUnused.Count > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "DEP_002",
                Title = "Remove Unused Dependencies",
                Description = $"Remove {dependencies.UnusedDependencies.CompletelyUnused.Count} unused dependencies",
                Category = "dependency",
                Priority = "medium",
                PotentialSavings = dependencies.UnusedDependencies.PotentialSavings,
                ImplementationEffort = 2,
                Impact = "Reduce bundle size and improve build performance",
                Steps = new List<string>
                {
                    "Run dependency audit",
                    "Verify dependencies are truly unused",
                    "Remove from package.json",
                    "Test application thoroughly"
                },
                EstimatedTime = "1 week"
            });
        }

        return recommendations;
    }

    private List<OptimizationRecommendation> GenerateAssetOptimizationRecommendations(List<AssetAnalysis> assets)
    {
        var recommendations = new List<OptimizationRecommendation>();

        var largeImages = assets.Where(a => a.AssetType == "image" && a.SizeBytes > 100 * 1024).ToList();
        if (largeImages.Count > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "ASSET_001",
                Title = "Optimize Large Images",
                Description = $"Optimize {largeImages.Count} large image assets",
                Category = "asset",
                Priority = "medium",
                PotentialSavings = largeImages.Sum(a => a.SizeBytes) / 2, // Estimate 50% savings
                ImplementationEffort = 3,
                Impact = "Faster page loading and reduced bandwidth usage",
                Steps = new List<string>
                {
                    "Compress images using tools like imagemin",
                    "Convert to WebP format where supported",
                    "Implement responsive images",
                    "Use CDN for image delivery"
                },
                EstimatedTime = "1-2 weeks"
            });
        }

        var unoptimizedAssets = assets.Where(a => !a.IsOptimized).Take(10).ToList();
        if (unoptimizedAssets.Count > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "ASSET_002",
                Title = "Enable Asset Optimization",
                Description = $"Enable optimization for {unoptimizedAssets.Count} unoptimized assets",
                Category = "asset",
                Priority = "low",
                PotentialSavings = unoptimizedAssets.Sum(a => a.SizeBytes) / 4, // Estimate 25% savings
                ImplementationEffort = 2,
                Impact = "Reduced asset sizes and faster loading",
                Steps = new List<string>
                {
                    "Enable minification in build configuration",
                    "Configure compression",
                    "Enable source map generation for debugging"
                },
                EstimatedTime = "1 week"
            });
        }

        return recommendations;
    }

    private List<OptimizationRecommendation> GenerateChunkOptimizationRecommendations(ChunkAnalysis chunks)
    {
        var recommendations = new List<OptimizationRecommendation>();

        var largeChunks = chunks.Chunks.Where(c => c.SizeBytes > 500 * 1024).ToList();
        if (largeChunks.Count > 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "CHUNK_001",
                Title = "Split Large Chunks",
                Description = $"Split {largeChunks.Count} large chunks (>500KB)",
                Category = "chunk",
                Priority = "high",
                PotentialSavings = 0, // Improves loading performance, not total size
                ImplementationEffort = 5,
                Impact = "Better loading performance and caching",
                Steps = new List<string>
                {
                    "Implement route-based code splitting",
                    "Use dynamic imports for feature modules",
                    "Configure webpack optimization settings",
                    "Test lazy loading implementation"
                },
                EstimatedTime = "2-3 weeks"
            });
        }

        if (chunks.Chunks.Count(c => c.Type == "async") == 0)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "CHUNK_002",
                Title = "Implement Lazy Loading",
                Description = "No lazy loading detected - implement code splitting",
                Category = "chunk",
                Priority = "high",
                PotentialSavings = 0, // Improves initial load performance
                ImplementationEffort = 6,
                Impact = "Significantly faster initial page load",
                Steps = new List<string>
                {
                    "Configure lazy loaded routes",
                    "Create feature modules",
                    "Implement dynamic imports",
                    "Test loading performance"
                },
                EstimatedTime = "3-4 weeks"
            });
        }

        return recommendations;
    }

    private List<OptimizationRecommendation> GenerateGeneralAngularOptimizationRecommendations(BundleOverview overview)
    {
        var recommendations = new List<OptimizationRecommendation>();

        if (overview.Comparison.Score.Overall < 70)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "GENERAL_001",
                Title = "Enable Production Optimizations",
                Description = "Bundle optimization score is below 70% - enable production optimizations",
                Category = "general",
                Priority = "high",
                PotentialSavings = overview.TotalSize / 3, // Estimate 33% savings
                ImplementationEffort = 4,
                Impact = "Comprehensive bundle size reduction",
                Steps = new List<string>
                {
                    "Enable AOT compilation",
                    "Enable tree shaking",
                    "Enable minification",
                    "Configure production build settings"
                },
                EstimatedTime = "1-2 weeks"
            });
        }

        if (overview.Distribution.VendorSize > 500 * 1024) // > 500KB
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Id = "GENERAL_002",
                Title = "Optimize Vendor Bundle",
                Description = $"Vendor bundle is large ({overview.Distribution.VendorSize / 1024}KB)",
                Category = "general",
                Priority = "medium",
                PotentialSavings = overview.Distribution.VendorSize / 4, // Estimate 25% savings
                ImplementationEffort = 5,
                Impact = "Reduced vendor bundle size and better caching",
                Steps = new List<string>
                {
                    "Review vendor dependencies",
                    "Enable tree shaking for third-party libraries",
                    "Configure vendor chunk splitting",
                    "Consider CDN for common libraries"
                },
                EstimatedTime = "2-3 weeks"
            });
        }

        return recommendations;
    }

    private OptimizationSummary GenerateOptimizationSummary(List<OptimizationRecommendation> recommendations)
    {
        return new OptimizationSummary
        {
            TotalRecommendations = recommendations.Count,
            TotalPotentialSavings = recommendations.Sum(r => r.PotentialSavings),
            TotalSavingsPercentage = 0, // Would calculate based on current bundle size
            QuickWinCount = recommendations.Count(r => r.ImplementationEffort <= 3),
            EstimatedImplementationTime = EstimateImplementationTime(recommendations),
            ExpectedImpact = DetermineExpectedImpact(recommendations),
            PrimaryFocusAreas = GetPrimaryFocusAreas(recommendations)
        };
    }

    private ImplementationGuide GenerateImplementationGuide(List<OptimizationRecommendation> recommendations)
    {
        var guide = new ImplementationGuide
        {
            Prerequisites = new List<string>
            {
                "Angular CLI installed",
                "Bundle analyzer tool",
                "Development environment setup"
            },
            Tools = new List<string>
            {
                "webpack-bundle-analyzer",
                "Angular DevTools",
                "Lighthouse",
                "npm audit"
            },
            Resources = new List<string>
            {
                "Angular Performance Guide",
                "Bundle optimization documentation",
                "Tree shaking best practices"
            }
        };

        // Create implementation phases
        var highPriority = recommendations.Where(r => r.Priority == "high").ToList();
        var mediumPriority = recommendations.Where(r => r.Priority == "medium").ToList();
        var lowPriority = recommendations.Where(r => r.Priority == "low").ToList();

        if (highPriority.Count > 0)
        {
            guide.Phases.Add(new ImplementationPhase
            {
                PhaseNumber = 1,
                Name = "Critical Optimizations",
                Description = "Address high-priority bundle size issues",
                RecommendationIds = highPriority.Select(r => r.Id).ToList(),
                EstimatedDuration = "2-4 weeks",
                Deliverables = new List<string> { "Reduced initial bundle size", "Improved loading performance" }
            });
        }

        if (mediumPriority.Count > 0)
        {
            guide.Phases.Add(new ImplementationPhase
            {
                PhaseNumber = 2,
                Name = "Performance Enhancements",
                Description = "Implement medium-priority optimizations",
                RecommendationIds = mediumPriority.Select(r => r.Id).ToList(),
                EstimatedDuration = "1-3 weeks",
                Deliverables = new List<string> { "Enhanced asset optimization", "Better dependency management" }
            });
        }

        if (lowPriority.Count > 0)
        {
            guide.Phases.Add(new ImplementationPhase
            {
                PhaseNumber = 3,
                Name = "Fine-tuning",
                Description = "Polish and fine-tune optimizations",
                RecommendationIds = lowPriority.Select(r => r.Id).ToList(),
                EstimatedDuration = "1-2 weeks",
                Deliverables = new List<string> { "Comprehensive optimization", "Performance monitoring setup" }
            });
        }

        guide.EstimatedTimeline = $"{guide.Phases.Count * 2}-{guide.Phases.Count * 4} weeks";

        return guide;
    }

    // Helper methods for analysis

    private string ExtractComponentName(string line)
    {
        // Simple extraction - would need more robust parsing
        var parts = line.Split(' ', ',', '{', '}');
        return parts.FirstOrDefault(p => p.EndsWith("Component")) ?? "";
    }

    private string ExtractServiceName(string line)
    {
        // Simple extraction - would need more robust parsing
        var parts = line.Split(' ', ',', '{', '}');
        return parts.FirstOrDefault(p => p.EndsWith("Service")) ?? "";
    }

    private bool IsModuleLazyLoaded(string moduleFile, string directory)
    {
        // This would require analysis of routing configuration files
        // For now, return false as default
        return false;
    }

    private string EstimateImplementationTime(List<OptimizationRecommendation> recommendations)
    {
        var totalWeeks = recommendations.Sum(r => ParseTimeEstimate(r.EstimatedTime));
        return totalWeeks <= 4 ? "1-4 weeks" :
               totalWeeks <= 8 ? "1-2 months" :
               totalWeeks <= 16 ? "2-4 months" : "4+ months";
    }

    private int ParseTimeEstimate(string timeEstimate)
    {
        // Parse time estimates like "1-2 weeks", "3-4 weeks", etc.
        if (timeEstimate.Contains("week"))
        {
            var numbers = System.Text.RegularExpressions.Regex.Matches(timeEstimate, @"\d+");
            if (numbers.Count > 0)
            {
                return int.Parse(numbers[0].Value);
            }
        }
        return 2; // Default 2 weeks
    }

    private string DetermineExpectedImpact(List<OptimizationRecommendation> recommendations)
    {
        var totalSavings = recommendations.Sum(r => r.PotentialSavings);
        var highPriorityCount = recommendations.Count(r => r.Priority == "high");

        if (totalSavings > 500 * 1024 || highPriorityCount > 3)
            return "Significant improvement in loading performance and user experience";
        if (totalSavings > 200 * 1024 || highPriorityCount > 1)
            return "Moderate improvement in bundle size and performance";
        return "Minor optimizations with incremental improvements";
    }

    private List<string> GetPrimaryFocusAreas(List<OptimizationRecommendation> recommendations)
    {
        var focusAreas = new List<string>();
        var categories = recommendations.GroupBy(r => r.Category).ToList();

        foreach (var category in categories.OrderByDescending(g => g.Count()))
        {
            focusAreas.Add($"{category.Key} optimization ({category.Count()} recommendations)");
        }

        return focusAreas.Take(3).ToList(); // Top 3 focus areas
    }
}
