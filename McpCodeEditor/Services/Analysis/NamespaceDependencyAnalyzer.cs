using System.Text.RegularExpressions;
using McpCodeEditor.Models.Analysis;

namespace McpCodeEditor.Services.Analysis
{
    /// <summary>
    /// Analyzes namespace dependencies through using statement analysis to detect coupling patterns and platform boundaries
    /// </summary>
    public class NamespaceDependencyAnalyzer(CodeEditorConfigurationService configService)
    {
        private static readonly Regex UsingStatementRegex = new Regex(
            @"^\s*using\s+([a-zA-Z_][a-zA-Z0-9_.]*)\s*;",
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Analyzes namespace dependencies across all C# files in the workspace
        /// </summary>
        public async Task<NamespaceDependencyAnalysis> AnalyzeNamespaceDependenciesAsync(string workspacePath)
        {
            var analysis = new NamespaceDependencyAnalysis();
            List<string> csharpFiles = GetCSharpFiles(workspacePath);

            // Step 1: Extract all using statements and their contexts
            var usingStatements = new List<UsingStatementContext>();
            foreach (string file in csharpFiles)
            {
                List<UsingStatementContext> fileUsings = await ExtractUsingStatementsAsync(file);
                usingStatements.AddRange(fileUsings);
            }

            // Step 2: Build namespace coupling relationships
            analysis.Couplings = BuildNamespaceCouplings(usingStatements);

            // Step 3: Detect platform boundaries
            analysis.PlatformBoundaries = DetectPlatformBoundaries(analysis.Couplings, workspacePath);

            // Step 4: Calculate coupling metrics and detect architectural patterns
            analysis.DetectedPatterns = DetectArchitecturalPatterns(analysis);

            return analysis;
        }

        /// <summary>
        /// Extract using statements from a C# file with context information
        /// </summary>
        private async Task<List<UsingStatementContext>> ExtractUsingStatementsAsync(string filePath)
        {
            var contexts = new List<UsingStatementContext>();
            
            try
            {
                string content = await File.ReadAllTextAsync(filePath);
                MatchCollection matches = UsingStatementRegex.Matches(content);

                // Extract the file's own namespace
                string fileNamespace = ExtractFileNamespace(content, filePath);

                foreach (Match match in matches)
                {
                    string usingNamespace = match.Groups[1].Value;
                    
                    // Skip global using statements and aliases
                    if (string.IsNullOrWhiteSpace(usingNamespace) || usingNamespace.Contains("="))
                        continue;

                    contexts.Add(new UsingStatementContext
                    {
                        FilePath = filePath,
                        SourceNamespace = fileNamespace,
                        TargetNamespace = usingNamespace,
                        IsSystemNamespace = IsSystemNamespace(usingNamespace),
                        IsProjectNamespace = IsProjectNamespace(usingNamespace, filePath)
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error but continue analysis
                Console.WriteLine($"Error analyzing file {filePath}: {ex.Message}");
            }

            return contexts;
        }

        /// <summary>
        /// Extract the primary namespace from a C# file
        /// </summary>
        private string ExtractFileNamespace(string content, string filePath)
        {
            // Try to find namespace declaration
            Match namespaceMatch = Regex.Match(content, @"namespace\s+([a-zA-Z_][a-zA-Z0-9_.]*)", RegexOptions.Multiline);
            if (namespaceMatch.Success)
            {
                return namespaceMatch.Groups[1].Value;
            }

            // Fallback: infer from file path
            return InferNamespaceFromPath(filePath);
        }

        /// <summary>
        /// Infer namespace from file path structure
        /// </summary>
        private string InferNamespaceFromPath(string filePath)
        {
            try
            {
                string workspacePath = configService.DefaultWorkspace;
                string relativePath = Path.GetRelativePath(workspacePath, filePath);
                string[] pathParts = Path.GetDirectoryName(relativePath)?.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries) ?? [];
                
                // Start with project name
                var namespaceParts = new List<string> { "McpCodeEditor" };
                namespaceParts.AddRange(pathParts.Where(p => !string.IsNullOrWhiteSpace(p) && p != "." && p != ".."));
                
                return string.Join(".", namespaceParts);
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Build namespace coupling relationships from using statement contexts
        /// </summary>
        private static List<NamespaceCoupling> BuildNamespaceCouplings(List<UsingStatementContext> usingStatements)
        {
            List<NamespaceCoupling> couplingGroups = usingStatements
                .Where(u => !u.IsSystemNamespace) // Focus on project and external package namespaces
                .GroupBy(u => new { u.SourceNamespace, u.TargetNamespace })
                .Select(g => new NamespaceCoupling
                {
                    SourceNamespace = g.Key.SourceNamespace,
                    TargetNamespace = g.Key.TargetNamespace,
                    UsageCount = g.Count(),
                    SourceFiles = g.Select(u => u.FilePath).Distinct().ToList(),
                    IsInternalCoupling = g.First().IsProjectNamespace,
                    SourcePlatform = ClassifyPlatform(g.Key.SourceNamespace),
                    TargetPlatform = ClassifyPlatform(g.Key.TargetNamespace)
                })
                .ToList();

            // Calculate coupling strength and cross-platform detection
            foreach (NamespaceCoupling coupling in couplingGroups)
            {
                coupling.CouplingStrength = CalculateCouplingStrength(coupling);
                coupling.IsCrossPlatformCoupling = IsCrossPlatformCoupling(coupling.SourcePlatform, coupling.TargetPlatform);
            }

            return couplingGroups;
        }

        /// <summary>
        /// Detect platform boundaries based on namespace patterns
        /// </summary>
        private static List<PlatformBoundary> DetectPlatformBoundaries(List<NamespaceCoupling> couplings, string workspacePath)
        {
            var platforms = new Dictionary<string, PlatformBoundary>();

            // Analyze all namespaces to identify platforms
            List<string> allNamespaces = couplings.SelectMany(c => new[] { c.SourceNamespace, c.TargetNamespace })
                .Where(ns => !IsSystemNamespace(ns))
                .Distinct()
                .ToList();

            foreach (string ns in allNamespaces)
            {
                string platform = ClassifyPlatform(ns);
                if (!platforms.ContainsKey(platform))
                {
                    platforms[platform] = new PlatformBoundary
                    {
                        PlatformName = platform,
                        NamespacePattern = GetNamespacePattern(ns, platform),
                        Type = ClassifyPlatformType(platform)
                    };
                }

                platforms[platform].Namespaces.Add(ns);
            }

            // Analyze coupling relationships for each platform
            foreach (PlatformBoundary platform in platforms.Values)
            {
                AnalyzePlatformCouplings(platform, couplings);
            }

            return platforms.Values.ToList();
        }

        /// <summary>
        /// Analyze coupling patterns for a specific platform
        /// </summary>
        private static void AnalyzePlatformCouplings(PlatformBoundary platform, List<NamespaceCoupling> allCouplings)
        {
            List<NamespaceCoupling> platformCouplings = allCouplings.Where(c => c.SourcePlatform == platform.PlatformName).ToList();

            // Internal couplings (within the platform)
            platform.InternalCouplingCount = platformCouplings.Count(c => c.TargetPlatform == platform.PlatformName);

            // External couplings (to other platforms)
            platform.ExternalCouplings = platformCouplings
                .Where(c => c.TargetPlatform != platform.PlatformName && !IsSystemNamespace(c.TargetNamespace))
                .Select(c => c.TargetNamespace)
                .Distinct()
                .ToList();

            // Shared dependencies (common namespaces)
            platform.SharedDependencies = platformCouplings
                .Where(c => IsSharedNamespace(c.TargetNamespace))
                .Select(c => c.TargetNamespace)
                .Distinct()
                .ToList();

            // Calculate isolation score
            int totalCouplings = platform.InternalCouplingCount + platform.ExternalCouplingCount;
            platform.IsolationScore = totalCouplings > 0 
                ? (double)platform.InternalCouplingCount / totalCouplings 
                : 1.0;
        }

        /// <summary>
        /// Detect architectural patterns based on namespace coupling analysis
        /// </summary>
        private static List<string> DetectArchitecturalPatterns(NamespaceDependencyAnalysis analysis)
        {
            var patterns = new List<string>();

            // Parallel Platform Strategy: Multiple platforms with high isolation
            List<PlatformBoundary> isolatedPlatforms = analysis.PlatformBoundaries.Where(p => p.IsolationScore > 0.8).ToList();
            if (isolatedPlatforms.Count >= 2)
            {
                patterns.Add($"Parallel Platform Strategy: {isolatedPlatforms.Count} isolated platforms detected");
            }

            // Shared Core Architecture: Common core with multiple consumers
            List<PlatformBoundary> sharedCorePlatforms = analysis.PlatformBoundaries.Where(p => p.Type == PlatformType.Core).ToList();
            if (sharedCorePlatforms.Count != 0)
            {
                int consumingPlatforms = analysis.Couplings
                    .Where(c => sharedCorePlatforms.Any(sp => sp.Namespaces.Contains(c.TargetNamespace)))
                    .Select(c => c.SourcePlatform)
                    .Distinct()
                    .Count();

                if (consumingPlatforms >= 2)
                {
                    patterns.Add($"Shared Core Architecture: Core consumed by {consumingPlatforms} platforms");
                }
            }

            // Layered Architecture: Clear dependency hierarchy
            if (DetectLayeredArchitecture(analysis))
            {
                patterns.Add("Layered Architecture: Clear dependency hierarchy detected");
            }

            return patterns;
        }

        // Helper methods
        private static bool IsSystemNamespace(string ns) => ns.StartsWith("System") || ns.StartsWith("Microsoft") || ns.StartsWith("Newtonsoft");
        private static bool IsProjectNamespace(string ns, string filePath) => ns.Contains("McpCodeEditor") || IsLocalProjectNamespace(ns, filePath);
        private static bool IsLocalProjectNamespace(string ns, string filePath) => Path.GetDirectoryName(filePath)?.Contains(ns.Split('.').LastOrDefault() ?? "") == true;
        private static bool IsSharedNamespace(string ns) => ns.Contains("Models") || ns.Contains("Common") || ns.Contains("Core") || ns.Contains("Shared");
        private static bool IsCrossPlatformCoupling(string sourcePlatform, string targetPlatform) => sourcePlatform != targetPlatform && !IsSharedNamespace(targetPlatform);

        private static string ClassifyPlatform(string ns)
        {
            string[] parts = ns.Split('.');
            if (parts.Length > 1)
            {
                string platformIndicator = parts[1].ToLowerInvariant();
                if (platformIndicator.Contains("angular") || platformIndicator.Contains("web")) return "Angular";
                if (platformIndicator.Contains("wpf") || platformIndicator.Contains("desktop")) return "WPF";
                if (platformIndicator.Contains("api") || platformIndicator.Contains("service")) return "API";
                if (platformIndicator.Contains("core") || platformIndicator.Contains("shared")) return "Core";
                if (platformIndicator.Contains("data") || platformIndicator.Contains("repository")) return "DataAccess";
                if (platformIndicator.Contains("test")) return "Tests";
                return platformIndicator;
            }
            return "Core";
        }

        private static PlatformType ClassifyPlatformType(string platform)
        {
            return platform.ToLowerInvariant() switch
            {
                "angular" or "react" or "vue" => PlatformType.Frontend,
                "wpf" or "winforms" or "desktop" => PlatformType.Desktop,
                "api" or "webapi" or "rest" => PlatformType.Api,
                "core" or "shared" or "common" => PlatformType.Core,
                "dataaccess" or "data" or "repository" => PlatformType.DataAccess,
                "services" or "business" => PlatformType.Services,
                "tests" or "test" => PlatformType.Tests,
                _ => PlatformType.Unknown
            };
        }

        private static string GetNamespacePattern(string ns, string platform)
        {
            string[] parts = ns.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{platform}";
            }
            return platform;
        }

        private static double CalculateCouplingStrength(NamespaceCoupling coupling)
        {
            // Base strength on usage count and file spread
            double baseStrength = Math.Min(coupling.UsageCount / 10.0, 1.0);
            double fileSpreadBonus = Math.Min(coupling.SourceFiles.Count / 5.0, 0.5);
            return Math.Min(baseStrength + fileSpreadBonus, 1.0);
        }

        private static bool DetectLayeredArchitecture(NamespaceDependencyAnalysis analysis)
        {
            // Check for typical layering patterns: Presentation -> Services -> DataAccess -> Models
            var layerTypes = new[] { PlatformType.Frontend, PlatformType.Services, PlatformType.DataAccess, PlatformType.Core };
            List<PlatformType> presentLayers = analysis.PlatformBoundaries.Select(p => p.Type).Intersect(layerTypes).ToList();
            
            return presentLayers.Count >= 3; // At least 3 layers for layered architecture
        }

        private static List<string> GetCSharpFiles(string workspacePath)
        {
            var excludeDirs = new[] { "bin", "obj", "node_modules", ".git", ".vs", "packages" };
            
            return Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !excludeDirs.Any(dir => f.Contains($"{Path.DirectorySeparatorChar}{dir}{Path.DirectorySeparatorChar}")))
                .ToList();
        }
    }

    /// <summary>
    /// Context information for a using statement
    /// </summary>
    public class UsingStatementContext
    {
        public string FilePath { get; set; } = string.Empty;
        public string SourceNamespace { get; set; } = string.Empty;
        public string TargetNamespace { get; set; } = string.Empty;
        public bool IsSystemNamespace { get; set; }
        public bool IsProjectNamespace { get; set; }
    }

    /// <summary>
    /// Complete namespace dependency analysis results
    /// </summary>
    public class NamespaceDependencyAnalysis
    {
        public List<NamespaceCoupling> Couplings { get; set; } = [];
        public List<PlatformBoundary> PlatformBoundaries { get; set; } = [];
        public List<string> DetectedPatterns { get; set; } = [];
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        public int TotalNamespaces => Couplings.SelectMany(c => new[] { c.SourceNamespace, c.TargetNamespace }).Distinct().Count();
        public int TotalCouplings => Couplings.Count;
        public double AverageCouplingStrength => Couplings.Count != 0 ? Couplings.Average(c => c.CouplingStrength) : 0.0;
    }
}
