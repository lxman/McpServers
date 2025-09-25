namespace McpCodeEditor.Services;

/// <summary>
/// Service for generating suggestion explanations and rationales.
/// Extracted from ContextAnalysisTools as part of RS-003 refactoring.
/// Provides configurable rationale templates and intelligent explanations.
/// </summary>
public class SuggestionRationaleService
{
    /// <summary>
    /// Generate a rationale explaining why specific files were suggested.
    /// </summary>
    /// <param name="analysis">Context analysis result containing project information</param>
    /// <param name="suggestions">List of suggested file paths</param>
    /// <returns>Human-readable explanation of the suggestion logic</returns>
    public static string GenerateSuggestionRationale(ContextAnalysisResult analysis, List<string> suggestions)
    {
        if (analysis.ProjectInfo == null)
            return "Files selected based on general relevance criteria.";

        var projectType = analysis.ProjectInfo.Type.ToString();
        var sourceFiles = CountSourceFiles(suggestions);
        var recentFiles = CountRecentFiles(suggestions);

        var rationale = $"Suggestions optimized for {projectType} project. ";
        
        if (sourceFiles > 0)
            rationale += $"Prioritized {sourceFiles} source code files. ";
        
        if (recentFiles > 0)
            rationale += $"Included {recentFiles} recently modified files. ";
        
        rationale += "Excluded backup files, build artifacts, and temporary files.";
        
        return rationale;
    }

    /// <summary>
    /// Generate detailed rationale with specific criteria explanations.
    /// </summary>
    /// <param name="analysis">Context analysis result</param>
    /// <param name="suggestions">List of suggested file paths</param>
    /// <param name="includeMetrics">Whether to include detailed metrics</param>
    /// <returns>Detailed explanation with metrics and criteria</returns>
    public static string GenerateDetailedRationale(ContextAnalysisResult analysis, List<string> suggestions, bool includeMetrics = true)
    {
        if (analysis.ProjectInfo == null)
            return "Files selected based on general relevance criteria without project context.";

        var metrics = AnalyzeSuggestionMetrics(suggestions);
        var projectType = analysis.ProjectInfo.Type.ToString();

        var rationale = $"File suggestions for {projectType} project:\n\n";
        
        // Project type specific rationale
        rationale += GetProjectTypeRationale(analysis.ProjectInfo.Type) + "\n\n";
        
        // File composition analysis
        rationale += $"Selected {suggestions.Count} files based on:\n";
        rationale += $"• Source code relevance: {metrics.SourceFiles} files\n";
        rationale += $"• Recency factor: {metrics.RecentFiles} recently modified\n";
        rationale += $"• Configuration importance: {metrics.ConfigFiles} config files\n";
        
        if (includeMetrics)
        {
            rationale += $"\nExclusion criteria applied:\n";
            rationale += "• Build artifacts (bin/, obj/, node_modules/)\n";
            rationale += "• Backup and temporary files (.bak, .tmp, .cache)\n";
            rationale += "• Generated files (auto-generated code)\n";
            rationale += "• Low relevance files (score < 40)\n";
        }

        return rationale;
    }

    /// <summary>
    /// Get project type specific rationale explanation.
    /// </summary>
    /// <param name="projectType">Detected project type</param>
    /// <returns>Project-specific explanation</returns>
    private static string GetProjectTypeRationale(ProjectType projectType)
    {
        return projectType switch
        {
            ProjectType.DotNet => "Prioritized C# source files (.cs), configuration files (.json, .config), and project files (.csproj, .sln).",
            ProjectType.React => "Focused on React components: JSX/TSX files, JavaScript, TypeScript, and React configuration files.",
            ProjectType.Angular => "Highlighted Angular components: TypeScript files, templates, styles, and Angular configuration.",
            ProjectType.Python => "Emphasized Python source files (.py), requirements files, and configuration (setup.py, pyproject.toml).",
            ProjectType.NodeJs => "Highlighted JavaScript/TypeScript files, package.json, and Node.js specific configuration.",
            ProjectType.Java => "Prioritized Java source files (.java), build files (pom.xml, build.gradle), and configuration.",
            ProjectType.Mixed => "Balanced approach for multi-technology project, prioritizing by file type relevance.",
            _ => "Applied general relevance criteria based on file types and modification patterns."
        };
    }

    /// <summary>
    /// Count source code files in the suggestions.
    /// </summary>
    /// <param name="suggestions">List of file paths</param>
    /// <returns>Number of source code files</returns>
    private static int CountSourceFiles(List<string> suggestions)
    {
        return suggestions.Count(s => 
            s.EndsWith(".cs") || s.EndsWith(".js") || s.EndsWith(".py") || 
            s.EndsWith(".java") || s.EndsWith(".cpp") || s.EndsWith(".c") ||
            s.EndsWith(".ts") || s.EndsWith(".tsx") || s.EndsWith(".jsx"));
    }

    /// <summary>
    /// Count recently modified files in the suggestions.
    /// </summary>
    /// <param name="suggestions">List of file paths</param>
    /// <returns>Number of recently modified files</returns>
    private static int CountRecentFiles(List<string> suggestions)
    {
        return suggestions.Count(s => 
        {
            try
            {
                var fileInfo = new FileInfo(s);
                return fileInfo.Exists && fileInfo.LastWriteTime > DateTime.Now.AddDays(-30);
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Analyze metrics about the suggested files.
    /// </summary>
    /// <param name="suggestions">List of file paths</param>
    /// <returns>Suggestion metrics</returns>
    private static SuggestionMetrics AnalyzeSuggestionMetrics(List<string> suggestions)
    {
        return new SuggestionMetrics
        {
            TotalFiles = suggestions.Count,
            SourceFiles = CountSourceFiles(suggestions),
            RecentFiles = CountRecentFiles(suggestions),
            ConfigFiles = suggestions.Count(s => 
                s.EndsWith(".json") || s.EndsWith(".xml") || s.EndsWith(".yaml") || 
                s.EndsWith(".yml") || s.EndsWith(".config") || s.EndsWith(".ini"))
        };
    }

    /// <summary>
    /// Metrics about suggested files for rationale generation.
    /// </summary>
    private class SuggestionMetrics
    {
        public int TotalFiles { get; set; }
        public int SourceFiles { get; set; }
        public int RecentFiles { get; set; }
        public int ConfigFiles { get; set; }
    }
}
