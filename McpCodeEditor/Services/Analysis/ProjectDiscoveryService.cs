using McpCodeEditor.Interfaces;

namespace McpCodeEditor.Services.Analysis;

/// <summary>
/// Service for discovering projects in directories and solution files
/// Single responsibility: Project discovery and solution file parsing
/// </summary>
public class ProjectDiscoveryService(ProjectDetectionService projectDetection) : IProjectDiscoveryService
{
    /// <summary>
    /// Get detailed project information for all projects in a directory tree
    /// Supports both solution-based and directory-based discovery
    /// </summary>
    public async Task<List<ProjectInfo>> GetProjectsInDirectoryAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var projects = new List<ProjectInfo>();

        try
        {
            // ENHANCEMENT 1: First try solution-based discovery
            var solutionFiles = Directory.GetFiles(rootPath, "*.sln", SearchOption.TopDirectoryOnly);
            if (solutionFiles.Any())
            {
                foreach (var solutionFile in solutionFiles)
                {
                    var solutionProjects = await ExtractProjectsFromSolutionAsync(solutionFile, cancellationToken);
                    projects.AddRange(solutionProjects);
                }
            }

            // ENHANCEMENT 2: ALWAYS do directory scanning (not just fallback)
            // This is the key fix - we need to scan directories even if solution exists
            await ScanDirectoriesForProjectsAsync(rootPath, projects, cancellationToken);

            // Also check the root directory itself
            var rootProject = await projectDetection.AnalyzeDirectoryAsync(rootPath);
            if (rootProject.Type != ProjectType.Unknown)
            {
                projects.Add(rootProject);
            }

            // Remove duplicates based on path
            projects = projects.GroupBy(p => p.Path).Select(g => g.First()).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning projects: {ex.Message}");
        }

        return projects;
    }

    /// <summary>
    /// Extract actual projects from solution file
    /// </summary>
    public async Task<List<ProjectInfo>> ExtractProjectsFromSolutionAsync(string solutionFile, CancellationToken cancellationToken = default)
    {
        var projects = new List<ProjectInfo>();
        
        try
        {
            var solutionContent = await File.ReadAllTextAsync(solutionFile, cancellationToken);
            var solutionDir = Path.GetDirectoryName(solutionFile)!;
            var lines = solutionContent.Split('\n');

            foreach (var line in lines)
            {
                // Look for project lines: Project("{GUID}") = "ProjectName", "RelativePath", "{ProjectGUID}"
                if (line.StartsWith("Project(") && (line.Contains(".csproj") || line.Contains(".esproj") || line.Contains(".vbproj")))
                {
                    var projectPath = ExtractProjectPathFromSolutionLine(line, solutionDir);
                    if (!string.IsNullOrEmpty(projectPath))
                    {
                        var projectDir = Path.GetDirectoryName(projectPath);
                        if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
                        {
                            var projectInfo = await projectDetection.AnalyzeDirectoryAsync(projectDir);
                            if (projectInfo.Type != ProjectType.Unknown)
                            {
                                projects.Add(projectInfo);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing solution file: {ex.Message}");
        }

        return projects;
    }

    /// <summary>
    /// Check if directory should be excluded from project scanning
    /// </summary>
    public bool IsExcludedDirectory(string directory)
    {
        var dirName = Path.GetFileName(directory).ToLowerInvariant();
        var excludedDirs = new[] { ".git", ".vs", ".vscode", "bin", "obj", "node_modules", "packages", "target", "dist", "build", ".angular" };
        return excludedDirs.Contains(dirName);
    }

    /// <summary>
    /// Check if directory seems like a project container that should be scanned more deeply
    /// </summary>
    public bool SeemsLikeProjectContainer(string directory)
    {
        var dirName = Path.GetFileName(directory).ToLowerInvariant();
        
        // Look for naming patterns that suggest this contains sub-projects
        var containerPatterns = new[]
        {
            "angular", "react", "frontend", "backend", "server", "client", 
            "web", "api", "wpf", "console", "shared", "common", "core",
            "app", "application", "service", "services", "ui"
        };

        return containerPatterns.Any(pattern => dirName.Contains(pattern));
    }

    /// <summary>
    /// Dedicated method for scanning directories recursively for projects
    /// </summary>
    private async Task ScanDirectoriesForProjectsAsync(string rootPath, List<ProjectInfo> projects, CancellationToken cancellationToken)
    {
        try
        {
            // Scan immediate subdirectories
            var directories = Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
                .Where(dir => !IsExcludedDirectory(dir))
                .Take(20); // Limit to avoid performance issues

            foreach (var directory in directories)
            {
                var projectInfo = await projectDetection.AnalyzeDirectoryAsync(directory);
                if (projectInfo.Type != ProjectType.Unknown)
                {
                    projects.Add(projectInfo);
                }

                // For directories that seem like project containers, scan TWO levels deeper
                if (SeemsLikeProjectContainer(directory))
                {
                    await ScanNestedProjectsAsync(directory, projects, 2, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in directory scanning: {ex.Message}");
        }
    }

    /// <summary>
    /// Recursively scan nested directories for projects
    /// </summary>
    private async Task ScanNestedProjectsAsync(string parentDir, List<ProjectInfo> projects, int maxDepth, CancellationToken cancellationToken)
    {
        if (maxDepth <= 0) return;

        try
        {
            var nestedDirs = Directory.GetDirectories(parentDir, "*", SearchOption.TopDirectoryOnly)
                .Where(dir => !IsExcludedDirectory(dir))
                .Take(10);

            foreach (var nestedDir in nestedDirs)
            {
                var nestedProject = await projectDetection.AnalyzeDirectoryAsync(nestedDir);
                if (nestedProject.Type != ProjectType.Unknown)
                {
                    projects.Add(nestedProject);
                }

                // Continue scanning deeper if needed
                if (maxDepth > 1 && SeemsLikeProjectContainer(nestedDir))
                {
                    await ScanNestedProjectsAsync(nestedDir, projects, maxDepth - 1, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in nested scanning: {ex.Message}");
        }
    }

    /// <summary>
    /// Extract project path from solution file line
    /// </summary>
    private static string ExtractProjectPathFromSolutionLine(string line, string solutionDir)
    {
        try
        {
            // Parse line like: Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ProjectName", "RelativePath\Project.csproj", "{ProjectGUID}"
            var parts = line.Split(',');
            if (parts.Length >= 2)
            {
                // Extract the relative path (second part, between quotes)
                var pathPart = parts[1].Trim();
                if (pathPart.StartsWith("\"") && pathPart.EndsWith("\""))
                {
                    var relativePath = pathPart.Substring(1, pathPart.Length - 2); // Remove quotes
                    return Path.Combine(solutionDir, relativePath);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing solution line: {ex.Message}");
        }

        return string.Empty;
    }
}
