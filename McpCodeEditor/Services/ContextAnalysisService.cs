using System.Text.RegularExpressions;

namespace McpCodeEditor.Services;

/// <summary>
/// Service for analyzing project context and providing intelligent file suggestions
/// Enhanced with UX-008: Uses ProjectScaleService for intelligent build artifact filtering
/// </summary>
public class ContextAnalysisService(
    CodeEditorConfigurationService config,
    ProjectDetectionService projectDetection,
    ProjectScaleService projectScale)
{
    // File type priorities (higher = more important)
    private readonly Dictionary<string, int> _fileTypePriorities = new()
    {
        // Source code files (highest priority)
        [".cs"] = 100,
        [".js"] = 95,
        [".ts"] = 95,
        [".jsx"] = 94,
        [".tsx"] = 94,
        [".py"] = 90,
        [".java"] = 90,
        [".cpp"] = 85,
        [".c"] = 85,
        [".h"] = 85,
        [".hpp"] = 85,
        [".go"] = 85,
        [".rs"] = 85,
        [".php"] = 80,
        [".rb"] = 80,
        [".swift"] = 80,
        [".kt"] = 80,
        [".scala"] = 80,
        
        // Configuration and project files (medium-high priority)
        [".json"] = 70,
        [".xml"] = 65,
        [".yaml"] = 65,
        [".yml"] = 65,
        [".toml"] = 65,
        [".config"] = 60,
        [".settings"] = 60,
        
        // Documentation and markup (medium priority)
        [".md"] = 50,
        [".readme"] = 50,
        [".rst"] = 45,
        [".adoc"] = 45,
        [".html"] = 40,
        [".htm"] = 40,
        [".css"] = 40,
        [".scss"] = 40,
        [".sass"] = 40,
        [".less"] = 40,
        
        // Scripts and build files (medium priority)
        [".sh"] = 35,
        [".bat"] = 35,
        [".ps1"] = 35,
        [".gradle"] = 35,
        [".make"] = 35,
        [".cmake"] = 35,
        
        // Data and resource files (low priority)
        [".sql"] = 30,
        [".csv"] = 25,
        [".txt"] = 20,
        [".log"] = 10,
        
        // Backup and temporary files (very low priority)
        [".bak"] = 5,
        [".tmp"] = 5,
        [".temp"] = 5,
        [".backup"] = 5
    };

    // Directories to exclude from analysis (should never be suggested)
    // NOTE: These are now supplemented by ProjectScaleService's more comprehensive filtering
    private readonly string[] _excludedDirectories =
    [
        ".mcp-backups",
        ".mcp-changes", 
        "bin",
        "obj",
        "node_modules",
        ".git",
        ".vs",
        ".vscode",
        "packages",
        "target",
        "dist",
        "build",
        ".angular",
        "__pycache__",
        ".pytest_cache",
        ".coverage",
        "coverage"
    ];

    // File patterns to exclude
    private readonly string[] _excludedFilePatterns =
    [
        @"\.mcp-backups",
        @"\.tmp$",
        @"\.temp$", 
        @"\.bak$",
        @"\.backup$",
        @"\.cache$",
        @"~$",
        @"\.log$",
        @"\.lock$"
    ];

    /// <summary>
    /// Analyzes current project context and returns relevant files prioritized by importance
    /// UX-008 Enhanced: Uses ProjectScaleService for intelligent build artifact filtering
    /// </summary>
    public async Task<ContextAnalysisResult> AnalyzeCurrentContextAsync(
        string? focusDirectory = null, 
        int maxFiles = 20)
    {
        var workspacePath = focusDirectory ?? config.DefaultWorkspace;
        
        if (!Directory.Exists(workspacePath))
        {
            return new ContextAnalysisResult
            {
                Success = false,
                Error = $"Directory does not exist: {workspacePath}",
                RelevantFiles = [],
                ProjectInfo = null
            };
        }

        try
        {
            // Get project information
            var projectInfo = await projectDetection.AnalyzeDirectoryAsync(workspacePath);
            
            // UX-008 ENHANCEMENT: Use ProjectScaleService for intelligent file discovery
            var relevantFiles = await GetRelevantFilesWithIntelligentFilteringAsync(workspacePath, maxFiles);
            
            return new ContextAnalysisResult
            {
                Success = true,
                WorkspacePath = workspacePath,
                ProjectInfo = projectInfo,
                RelevantFiles = relevantFiles,
                TotalFilesAnalyzed = relevantFiles.Count,
                FilteringApplied = new FilteringInfo
                {
                    ExcludedDirectories = _excludedDirectories,
                    ExcludedPatterns = _excludedFilePatterns,
                    FileTypePriorities = _fileTypePriorities.Where(kvp => kvp.Value >= 30).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                }
            };
        }
        catch (Exception ex)
        {
            return new ContextAnalysisResult
            {
                Success = false,
                Error = ex.Message,
                RelevantFiles = [],
                ProjectInfo = null
            };
        }
    }

    /// <summary>
    /// Gets personalized file suggestions based on project type and recent activity
    /// </summary>
    public async Task<List<string>> GetPersonalizedSuggestionsAsync(
        string? focusDirectory = null,
        int maxSuggestions = 10)
    {
        var context = await AnalyzeCurrentContextAsync(focusDirectory, maxSuggestions * 2);
        
        if (!context.Success || context.RelevantFiles == null)
        {
            return [];
        }

        // Prioritize based on project type and recency
        return context.RelevantFiles
            .Where(f => f.RelevanceScore >= 50) // Only suggest highly relevant files
            .OrderByDescending(f => f.RelevanceScore)
            .ThenByDescending(f => f.LastModified)
            .Take(maxSuggestions)
            .Select(f => f.FilePath)
            .ToList();
    }

    /// <summary>
    /// UX-008 ENHANCED: Uses ProjectScaleService to get intelligently filtered files,
    /// then applies context-specific relevance scoring
    /// </summary>
    private async Task<List<FileRelevanceInfo>> GetRelevantFilesWithIntelligentFilteringAsync(string directoryPath, int maxFiles)
    {
        var relevantFiles = new List<FileRelevanceInfo>();
        
        // UX-008: Use ProjectScaleService for intelligent build artifact filtering
        var scaleAnalysis = await ProjectScaleService.AnalyzeProjectScaleAsync(directoryPath);
        
        // Combine source files and other files (excluding build artifacts)
        var filesToAnalyze = new List<ProjectFile>();
        filesToAnalyze.AddRange(scaleAnalysis.SourceFiles);
        filesToAnalyze.AddRange(scaleAnalysis.OtherFiles); // Include config files, docs, etc.
        // Note: We intentionally exclude BuildArtifacts and GeneratedFiles for better context
        
        foreach (var projectFile in filesToAnalyze)
        {
            var filePath = Path.Combine(directoryPath, projectFile.RelativePath);
            
            // Skip if file matches our additional excluded patterns (belt and suspenders)
            if (MatchesExcludedPattern(filePath))
                continue;
                
            // Calculate relevance score using our context-specific logic
            var relevanceScore = CalculateFileRelevance(filePath);
            
            // Only include files with reasonable relevance
            if (relevanceScore >= 20)
            {
                var fileInfo = new FileInfo(filePath);
                relevantFiles.Add(new FileRelevanceInfo
                {
                    FilePath = filePath,
                    RelativePath = projectFile.RelativePath,
                    FileType = Path.GetExtension(filePath).ToLowerInvariant(),
                    RelevanceScore = relevanceScore,
                    LastModified = fileInfo.LastWriteTime,
                    SizeBytes = fileInfo.Length,
                    ReasonForRelevance = GetRelevanceReason(filePath, relevanceScore)
                });
            }
        }
        
        // Sort by relevance score and return top files
        return relevantFiles
            .OrderByDescending(f => f.RelevanceScore)
            .ThenByDescending(f => f.LastModified)
            .Take(maxFiles)
            .ToList();
    }

    /// <summary>
    /// LEGACY METHOD: Kept for fallback compatibility
    /// </summary>
    private async Task<List<FileRelevanceInfo>> GetRelevantFilesAsync(string directoryPath, int maxFiles)
    {
        var relevantFiles = new List<FileRelevanceInfo>();
        
        // Get all files recursively (OLD APPROACH - includes build artifacts)
        var allFiles = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        
        foreach (var filePath in allFiles)
        {
            // Skip if file is in excluded directory
            if (IsInExcludedDirectory(filePath, directoryPath))
                continue;
                
            // Skip if file matches excluded patterns
            if (MatchesExcludedPattern(filePath))
                continue;
                
            // Calculate relevance score
            var relevanceScore = CalculateFileRelevance(filePath);
            
            // Only include files with reasonable relevance
            if (relevanceScore >= 20)
            {
                var fileInfo = new FileInfo(filePath);
                relevantFiles.Add(new FileRelevanceInfo
                {
                    FilePath = filePath,
                    RelativePath = Path.GetRelativePath(directoryPath, filePath),
                    FileType = Path.GetExtension(filePath).ToLowerInvariant(),
                    RelevanceScore = relevanceScore,
                    LastModified = fileInfo.LastWriteTime,
                    SizeBytes = fileInfo.Length,
                    ReasonForRelevance = GetRelevanceReason(filePath, relevanceScore)
                });
            }
        }
        
        // Sort by relevance score and return top files
        return relevantFiles
            .OrderByDescending(f => f.RelevanceScore)
            .ThenByDescending(f => f.LastModified)
            .Take(maxFiles)
            .ToList();
    }

    private bool IsInExcludedDirectory(string filePath, string basePath)
    {
        var relativePath = Path.GetRelativePath(basePath, filePath);
        var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        return pathParts.Any(part => _excludedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private bool MatchesExcludedPattern(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        return _excludedFilePatterns.Any(pattern => Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase));
    }

    private int CalculateFileRelevance(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        
        // Base score from file type
        var score = _fileTypePriorities.GetValueOrDefault(extension, 15);
        
        // Bonus for important file names
        if (fileName.Contains("readme") || fileName.Contains("main") || fileName.Contains("index"))
            score += 20;
        if (fileName.Contains("config") || fileName.Contains("settings"))
            score += 15;
        if (fileName.Contains("test") || fileName.Contains("spec"))
            score += 10;
        
        // Penalty for generated/build files (additional layer on top of ProjectScaleService filtering)
        if (fileName.Contains("generated") || fileName.Contains(".designer.") || fileName.Contains(".g."))
            score -= 30;
        if (fileName.Contains(".min.") || fileName.Contains(".bundle."))
            score -= 20;
        
        // Bonus for recently modified files
        var fileInfo = new FileInfo(filePath);
        var age = DateTime.Now - fileInfo.LastWriteTime;
        if (age.TotalDays < 7)
            score += 15;
        else if (age.TotalDays < 30)
            score += 10;
        else if (age.TotalDays < 90)
            score += 5;
        
        // Bonus for reasonable file sizes (not too small, not too large)
        var sizeKb = fileInfo.Length / 1024;
        if (sizeKb is > 1 and < 1000) // 1KB to 1MB is reasonable for source files
            score += 5;
        else if (sizeKb > 5000) // Very large files are less likely to be relevant
            score -= 10;
        
        return Math.Max(0, score);
    }

    private string GetRelevanceReason(string filePath, int score)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        
        var reasons = new List<string>();
        
        if (_fileTypePriorities.GetValueOrDefault(extension, 0) >= 80)
            reasons.Add("Primary source code file");
        else if (_fileTypePriorities.GetValueOrDefault(extension, 0) >= 60)
            reasons.Add("Configuration/project file");
        else if (_fileTypePriorities.GetValueOrDefault(extension, 0) >= 40)
            reasons.Add("Documentation/markup file");
        
        if (fileName.Contains("readme") || fileName.Contains("main") || fileName.Contains("index"))
            reasons.Add("Important project file");
        
        var fileInfo = new FileInfo(filePath);
        var age = DateTime.Now - fileInfo.LastWriteTime;
        if (age.TotalDays < 30)
            reasons.Add("Recently modified");
        
        // Add UX-008 note
        reasons.Add("Intelligently filtered (build artifacts excluded)");
        
        return reasons.Count != 0 ? string.Join(", ", reasons) : "Standard project file";
    }
}

/// <summary>
/// Information about a file's relevance to the current context
/// </summary>
public class FileRelevanceInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public int RelevanceScore { get; set; }
    public DateTime LastModified { get; set; }
    public long SizeBytes { get; set; }
    public string ReasonForRelevance { get; set; } = string.Empty;
}

/// <summary>
/// Result of context analysis
/// </summary>
public class ContextAnalysisResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string WorkspacePath { get; set; } = string.Empty;
    public ProjectInfo? ProjectInfo { get; set; }
    public List<FileRelevanceInfo>? RelevantFiles { get; set; }
    public int TotalFilesAnalyzed { get; set; }
    public FilteringInfo? FilteringApplied { get; set; }
}

/// <summary>
/// Information about filtering applied during context analysis
/// </summary>
public class FilteringInfo
{
    public string[] ExcludedDirectories { get; set; } = [];
    public string[] ExcludedPatterns { get; set; } = [];
    public Dictionary<string, int> FileTypePriorities { get; set; } = new();
}
