using System.Text.RegularExpressions;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for intelligent project scale detection and build artifact filtering.
/// Addresses UX-008: Distinguishes between source files and generated/dependency files
/// to provide accurate project complexity metrics.
/// </summary>
public class ProjectScaleService(CodeEditorConfigurationService config)
{
    private readonly CodeEditorConfigurationService _config = config;
    
    // Build artifact patterns to exclude from source file counts
    private static readonly string[] BuildArtifactPatterns =
    [
        // Node.js / JavaScript
        "node_modules/",
        "npm_modules/",
        "dist/",
        "build/",
        ".next/",
        ".nuxt/",
        ".angular/cache/",
        "coverage/",
        ".nyc_output/",
        
        // .NET
        "bin/",
        "obj/",
        "packages/",
        ".vs/",
        
        // Java
        "target/",
        ".m2/",
        
        // Python
        "__pycache__/",
        ".pytest_cache/",
        "venv/",
        "env/",
        ".env/",
        
        // General
        ".git/",
        ".svn/",
        ".hg/",
        "temp/",
        "tmp/",
        "cache/",
        ".cache/",
        "logs/",
        ".idea/",
        ".vscode/",
        
        // Documentation builds
        "_site/",
        "docs/_build/",
        "site/",
        
        // Mobile
        "ios/Pods/",
        "android/.gradle/",
        "flutter/.pub-cache/",
        
        // Database
        "*.db",
        "*.sqlite",
        "*.sqlite3"
    ];
    
    // Generated file patterns
    private static readonly string[] GeneratedFilePatterns =
    [
        "*.generated.cs",
        "*.designer.cs",
        "*.Designer.cs",
        "*.g.cs",
        "*.g.i.cs",
        "AssemblyInfo.cs",
        "GlobalAssemblyInfo.cs",
        "*.min.js",
        "*.min.css",
        "*.bundle.js",
        "*.bundle.css",
        "*.map",
        "*.d.ts",
        "webpack.config.js",
        "tsconfig.json",
        "package-lock.json",
        "yarn.lock",
        "*.csproj.user",
        "*.sln.DotSettings.user"
    ];
    
    // Source file extensions (what we consider "real" code)
    private static readonly string[] SourceFileExtensions =
    [
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".vue", ".svelte",
        ".py", ".java", ".kt", ".scala", ".go", ".rs", ".cpp", 
        ".cc", ".cxx", ".c", ".h", ".hpp", ".php", ".rb", 
        ".swift", ".dart", ".sql", ".html", ".razor", ".cshtml"
    ];

    /// <summary>
    /// Analyzes project scale with intelligent filtering of build artifacts
    /// </summary>
    public static async Task<ProjectScaleAnalysis> AnalyzeProjectScaleAsync(string projectPath)
    {
        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"Project path does not exist: {projectPath}");
        }
        
        string[] allFiles = Directory.GetFiles(projectPath, "*", SearchOption.AllDirectories);
        var analysis = new ProjectScaleAnalysis
        {
            ProjectPath = projectPath,
            AnalyzedAt = DateTime.UtcNow
        };
        
        // Categorize all files
        foreach (string file in allFiles)
        {
            string relativePath = Path.GetRelativePath(projectPath, file);
            var fileInfo = new FileInfo(file);
            FileCategory category = CategorizeFile(relativePath, fileInfo);
            
            analysis.AddFile(relativePath, fileInfo.Length, category);
        }

        // Calculate metrics
        ProjectScaleAnalysis.CalculateMetrics();
        
        return analysis;
    }
    
    /// <summary>
    /// Gets source file statistics excluding build artifacts
    /// </summary>
    public static async Task<SourceFileStatistics> GetSourceStatisticsAsync(string projectPath)
    {
        ProjectScaleAnalysis analysis = await AnalyzeProjectScaleAsync(projectPath);
        
        return new SourceFileStatistics
        {
            ProjectPath = projectPath,
            SourceFileCount = analysis.SourceFiles.Count,
            GeneratedFileCount = analysis.GeneratedFiles.Count,
            BuildArtifactCount = analysis.BuildArtifacts.Count,
            TotalFileCount = analysis.TotalFiles,
            SourceSizeBytes = analysis.SourceFiles.Sum(f => f.SizeBytes),
            TotalSizeBytes = analysis.TotalSizeBytes,
            SourceComplexityScore = CalculateSourceComplexity(analysis),
            ProjectScale = DetermineProjectScale(analysis),
            LanguageBreakdown = GetLanguageBreakdown(analysis.SourceFiles),
            ExcludedPatterns = BuildArtifactPatterns.ToList()
        };
    }
    
    /// <summary>
    /// Detects if build artifacts are significantly skewing file counts
    /// </summary>
    public static async Task<BuildArtifactDetectionResult> DetectBuildArtifactsAsync(string projectPath)
    {
        ProjectScaleAnalysis analysis = await AnalyzeProjectScaleAsync(projectPath);
        
        var result = new BuildArtifactDetectionResult
        {
            ProjectPath = projectPath,
            TotalFiles = analysis.TotalFiles,
            SourceFiles = analysis.SourceFiles.Count,
            BuildArtifacts = analysis.BuildArtifacts.Count,
            GeneratedFiles = analysis.GeneratedFiles.Count,
            ArtifactPercentage = analysis.TotalFiles > 0 ? 
                (double)(analysis.BuildArtifacts.Count + analysis.GeneratedFiles.Count) / analysis.TotalFiles * 100 : 0,
            RecommendExclusion = analysis.BuildArtifacts.Count > analysis.SourceFiles.Count,
            LargestArtifactCategories = GetLargestArtifactCategories(analysis)
        };
        
        return result;
    }
    
    private static FileCategory CategorizeFile(string relativePath, FileInfo fileInfo)
    {
        string normalizedPath = relativePath.Replace('\\', '/');
        
        // Check for build artifacts first (directory-based)
        if (IsBuildArtifact(normalizedPath))
        {
            return FileCategory.BuildArtifact;
        }
        
        // Check for generated files
        if (IsGeneratedFile(relativePath))
        {
            return FileCategory.Generated;
        }
        
        // Check if it's a source file
        string extension = Path.GetExtension(relativePath).ToLowerInvariant();
        if (SourceFileExtensions.Contains(extension))
        {
            return FileCategory.Source;
        }
        
        // Everything else is considered other (configs, docs, etc.)
        return FileCategory.Other;
    }
    
    private static bool IsBuildArtifact(string normalizedPath)
    {
        return BuildArtifactPatterns.Any(pattern => 
        {
            if (pattern.EndsWith("/"))
            {
                // Directory pattern
                return normalizedPath.Contains(pattern) || normalizedPath.StartsWith(pattern);
            }
            else
            {
                // File pattern (could contain wildcards)
                return normalizedPath.Contains(pattern.Replace("*", ""));
            }
        });
    }
    
    private static bool IsGeneratedFile(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath);
        
        return GeneratedFilePatterns.Any(pattern =>
        {
            if (pattern.Contains("*"))
            {
                var regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", 
                    RegexOptions.IgnoreCase);
                return regex.IsMatch(fileName);
            }
            else
            {
                return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
            }
        });
    }
    
    private static int CalculateSourceComplexity(ProjectScaleAnalysis analysis)
    {
        int sourceFiles = analysis.SourceFiles.Count;
        
        // Basic complexity scoring
        if (sourceFiles < 10) return 1;           // Very Simple
        if (sourceFiles < 50) return 2;           // Simple  
        if (sourceFiles < 200) return 3;          // Medium
        if (sourceFiles < 500) return 4;          // Complex
        if (sourceFiles < 1000) return 5;         // Very Complex
        return 6;                                 // Enterprise
    }
    
    private static ProjectScale DetermineProjectScale(ProjectScaleAnalysis analysis)
    {
        int sourceFiles = analysis.SourceFiles.Count;
        
        return sourceFiles switch
        {
            < 10 => ProjectScale.Tiny,
            < 50 => ProjectScale.Small,
            < 200 => ProjectScale.Medium,
            < 500 => ProjectScale.Large,
            < 1000 => ProjectScale.VeryLarge,
            _ => ProjectScale.Enterprise
        };
    }
    
    private static Dictionary<string, int> GetLanguageBreakdown(List<ProjectFile> sourceFiles)
    {
        return sourceFiles
            .GroupBy(f => Path.GetExtension(f.RelativePath).ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Count());
    }
    
    private static List<ArtifactCategory> GetLargestArtifactCategories(ProjectScaleAnalysis analysis)
    {
        var categories = new Dictionary<string, int>();
        
        foreach (ProjectFile artifact in analysis.BuildArtifacts)
        {
            string category = GetArtifactCategory(artifact.RelativePath);
            categories[category] = categories.GetValueOrDefault(category, 0) + 1;
        }
        
        return categories
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => new ArtifactCategory { Name = kvp.Key, FileCount = kvp.Value })
            .ToList();
    }
    
    private static string GetArtifactCategory(string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/');
        
        if (normalizedPath.Contains("node_modules/")) return "Node.js Dependencies";
        if (normalizedPath.Contains("bin/") || normalizedPath.Contains("obj/")) return ".NET Build Output";
        if (normalizedPath.Contains("target/")) return "Java Build Output";
        if (normalizedPath.Contains("__pycache__/")) return "Python Cache";
        if (normalizedPath.Contains(".git/")) return "Git Repository";
        if (normalizedPath.Contains("packages/")) return "Package Dependencies";
        if (normalizedPath.Contains(".angular/")) return "Angular Cache";
        if (normalizedPath.Contains("dist/") || normalizedPath.Contains("build/")) return "Build Distribution";
        
        return "Other Build Artifacts";
    }
}

#region Data Models

public class ProjectScaleAnalysis
{
    public string ProjectPath { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public int TotalFiles { get; private set; }
    public long TotalSizeBytes { get; private set; }
    
    public List<ProjectFile> SourceFiles { get; } = [];
    public List<ProjectFile> GeneratedFiles { get; } = [];
    public List<ProjectFile> BuildArtifacts { get; } = [];
    public List<ProjectFile> OtherFiles { get; } = [];
    
    public void AddFile(string relativePath, long sizeBytes, FileCategory category)
    {
        var file = new ProjectFile
        {
            RelativePath = relativePath,
            SizeBytes = sizeBytes,
            Category = category
        };
        
        TotalFiles++;
        TotalSizeBytes += sizeBytes;
        
        switch (category)
        {
            case FileCategory.Source:
                SourceFiles.Add(file);
                break;
            case FileCategory.Generated:
                GeneratedFiles.Add(file);
                break;
            case FileCategory.BuildArtifact:
                BuildArtifacts.Add(file);
                break;
            case FileCategory.Other:
                OtherFiles.Add(file);
                break;
        }
    }
    
    public static void CalculateMetrics()
    {
        // Additional metrics calculations can be added here
    }
}

public class ProjectFile
{
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public FileCategory Category { get; set; }
}

public class SourceFileStatistics
{
    public string ProjectPath { get; set; } = string.Empty;
    public int SourceFileCount { get; set; }
    public int GeneratedFileCount { get; set; }
    public int BuildArtifactCount { get; set; }
    public int TotalFileCount { get; set; }
    public long SourceSizeBytes { get; set; }
    public long TotalSizeBytes { get; set; }
    public int SourceComplexityScore { get; set; }
    public ProjectScale ProjectScale { get; set; }
    public Dictionary<string, int> LanguageBreakdown { get; set; } = new();
    public List<string> ExcludedPatterns { get; set; } = [];
}

public class BuildArtifactDetectionResult
{
    public string ProjectPath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int SourceFiles { get; set; }
    public int BuildArtifacts { get; set; }
    public int GeneratedFiles { get; set; }
    public double ArtifactPercentage { get; set; }
    public bool RecommendExclusion { get; set; }
    public List<ArtifactCategory> LargestArtifactCategories { get; set; } = [];
}

public class ArtifactCategory
{
    public string Name { get; set; } = string.Empty;
    public int FileCount { get; set; }
}

public enum FileCategory
{
    Source,
    Generated,
    BuildArtifact,
    Other
}

public enum ProjectScale
{
    Tiny,       // < 10 files
    Small,      // 10-49 files  
    Medium,     // 50-199 files
    Large,      // 200-499 files
    VeryLarge,  // 500-999 files
    Enterprise  // 1000+ files
}

#endregion
