using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;

namespace McpCodeEditor.Services.Analysis;

/// <summary>
/// Service for detecting architecture patterns using various detection strategies
/// Phase 4 - Service Layer Cleanup: Extracted from ArchitectureDetectionService
/// Single responsibility: Pattern detection using different strategic approaches
/// </summary>
public class PatternDetectionStrategyService : IPatternDetectionStrategyService
{
    /// <summary>
    /// Detect patterns based on solution files and project combinations within solutions
    /// </summary>
    public async Task<List<ArchitecturePattern>> DetectSolutionBasedPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<ArchitecturePattern>();

        try
        {
            // Find solution files
            string[] solutionFiles = Directory.GetFiles(rootPath, "*.sln", SearchOption.TopDirectoryOnly);

            foreach (string solutionFile in solutionFiles)
            {
                ArchitecturePattern? solutionPattern = await AnalyzeSolutionFileAsync(solutionFile, projects, cancellationToken);
                if (solutionPattern != null)
                {
                    patterns.Add(solutionPattern);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in solution-based detection: {ex.Message}");
        }

        return patterns;
    }

    /// <summary>
    /// Detect directory-based patterns like monorepos and separated frontend/backend
    /// </summary>
    public async Task<List<ArchitecturePattern>> DetectDirectoryBasedPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<ArchitecturePattern>();

        try
        {
            // Check for monorepo pattern
            if (projects.Count >= 3 && HasMonorepoIndicators(rootPath))
            {
                var monorepoPattern = new ArchitecturePattern
                {
                    Type = ArchitectureType.MonoRepoMultiProject,
                    Name = "MonoRepo Multi-Project",
                    Description = $"Multiple projects ({projects.Count}) in single repository",
                    RootPath = rootPath,
                    ProjectPaths = projects.Select(p => p.Path).ToList(),
                    Technologies = projects.SelectMany(p => p.Indicators).Distinct().ToList(),
                    DetectionReasons = ["Multiple project types in single directory", $"{projects.Count} projects detected"],
                    Indicators = [
                        new PatternIndicator
                        {
                            Type = "project_count",
                            Value = projects.Count.ToString(),
                            Location = rootPath,
                            Weight = 1.0,
                            Description = $"{projects.Count} projects in single repository"
                        }
                    ]
                };

                patterns.Add(monorepoPattern);
            }

            // Check for separated frontend/backend pattern
            List<ProjectInfo> frontendProjects = projects.Where(p => p.Type is ProjectType.Angular or ProjectType.React).ToList();
            List<ProjectInfo> backendProjects = projects.Where(p => p.Type is ProjectType.DotNet or ProjectType.NodeJs).ToList();

            if (frontendProjects.Count != 0 && backendProjects.Count != 0)
            {
                var separatedPattern = new ArchitecturePattern
                {
                    Type = ArchitectureType.FrontendBackendSeparated,
                    Name = "Separated Frontend/Backend",
                    Description = "Frontend and backend projects in separate directories",
                    RootPath = rootPath,
                    ProjectPaths = frontendProjects.Concat(backendProjects).Select(p => p.Path).ToList(),
                    Technologies = frontendProjects.Concat(backendProjects).SelectMany(p => p.Indicators).Distinct().ToList(),
                    DetectionReasons = [$"{frontendProjects.Count} frontend project(s)", $"{backendProjects.Count} backend project(s)"]
                };

                patterns.Add(separatedPattern);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in directory-based detection: {ex.Message}");
        }

        return patterns;
    }

    /// <summary>
    /// Detect patterns based on naming conventions and project names
    /// </summary>
    public async Task<List<ArchitecturePattern>> DetectNamingBasedPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<ArchitecturePattern>();

        try
        {
            // Look for common naming patterns
            List<string> projectNames = projects.Select(p => Path.GetFileName(p.Path).ToLowerInvariant()).ToList();
            
            // Check for MCP server/client pattern
            List<string> mcpServers = projectNames.Where(name => name.Contains("mcp") && name.Contains("server")).ToList();
            List<string> mcpClients = projectNames.Where(name => name.Contains("mcp") && name.Contains("client")).ToList();

            if (mcpServers.Count != 0 || mcpClients.Count != 0)
            {
                var mcpPattern = new ArchitecturePattern
                {
                    Type = ArchitectureType.McpServerClient,
                    Name = "MCP Server/Client",
                    Description = "Model Context Protocol server and client architecture",
                    RootPath = rootPath,
                    ProjectPaths = projects.Where(p => 
                        mcpServers.Contains(Path.GetFileName(p.Path).ToLowerInvariant()) ||
                        mcpClients.Contains(Path.GetFileName(p.Path).ToLowerInvariant()))
                        .Select(p => p.Path).ToList(),
                    Technologies = ["MCP", "Model Context Protocol"],
                    DetectionReasons = mcpServers.Concat(mcpClients).Select(name => $"Project name: {name}").ToList()
                };

                patterns.Add(mcpPattern);
            }

            // Check for shared library patterns
            List<string> sharedLibs = projectNames.Where(name => 
                name.Contains("shared") || name.Contains("common") || name.Contains("core")).ToList();

            if (sharedLibs.Count != 0 && projects.Count > sharedLibs.Count)
            {
                var sharedPattern = new ArchitecturePattern
                {
                    Type = ArchitectureType.WpfDotNetSharedLibs,
                    Name = "Shared Libraries Architecture",
                    Description = "Projects with shared/common libraries",
                    RootPath = rootPath,
                    ProjectPaths = projects.Select(p => p.Path).ToList(),
                    Technologies = projects.SelectMany(p => p.Indicators).Distinct().ToList(),
                    DetectionReasons = sharedLibs.Select(name => $"Shared library: {name}").ToList()
                };

                patterns.Add(sharedPattern);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in naming-based detection: {ex.Message}");
        }

        return patterns;
    }

    /// <summary>
    /// Detect patterns based on direct project type combinations
    /// </summary>
    public async Task<List<ArchitecturePattern>> DetectProjectCombinationPatternsAsync(
        string rootPath, 
        List<ProjectInfo> projects, 
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<ArchitecturePattern>();

        try
        {
            // Check for Angular + .NET combination
            List<ProjectInfo> angularProjects = projects.Where(p => p.Type == ProjectType.Angular).ToList();
            List<ProjectInfo> dotNetProjects = projects.Where(p => p.Type == ProjectType.DotNet).ToList();

            if (angularProjects.Count != 0 && dotNetProjects.Count != 0)
            {
                var pattern = new ArchitecturePattern
                {
                    Type = ArchitectureType.AngularDotNetApi,
                    Name = "Angular + .NET API",
                    Description = $"Angular frontend ({angularProjects.Count}) with .NET backend API ({dotNetProjects.Count})",
                    RootPath = rootPath,
                    ProjectPaths = angularProjects.Concat(dotNetProjects).Select(p => p.Path).ToList(),
                    Technologies = ["Angular", ".NET", "C#", "TypeScript"],
                    DetectionReasons = [
                        $"Angular projects: {string.Join(", ", angularProjects.Select(p => p.Name))}",
                        $".NET projects: {string.Join(", ", dotNetProjects.Select(p => p.Name))}",
                        "Multi-platform architecture detected"
                    ],
                    Indicators = angularProjects.Concat(dotNetProjects).SelectMany(p => p.Indicators.Select(indicator => new PatternIndicator
                    {
                        Type = "project_indicator",
                        Value = indicator,
                        Location = p.Path,
                        Weight = 2.0, // Higher weight for direct detection
                        Description = $"{p.Type} project indicator"
                    })).ToList()
                };

                patterns.Add(pattern);
            }

            // Check for React + Node.js combination
            List<ProjectInfo> reactProjects = projects.Where(p => p.Type == ProjectType.React).ToList();
            List<ProjectInfo> nodeProjects = projects.Where(p => p.Type == ProjectType.NodeJs).ToList();

            if (reactProjects.Count != 0 && nodeProjects.Count != 0)
            {
                var pattern = new ArchitecturePattern
                {
                    Type = ArchitectureType.ReactNodeJsDatabase,
                    Name = "React + Node.js",
                    Description = $"React frontend ({reactProjects.Count}) with Node.js backend ({nodeProjects.Count})",
                    RootPath = rootPath,
                    ProjectPaths = reactProjects.Concat(nodeProjects).Select(p => p.Path).ToList(),
                    Technologies = ["React", "Node.js", "JavaScript", "TypeScript"],
                    DetectionReasons = [
                        $"React projects: {string.Join(", ", reactProjects.Select(p => p.Name))}",
                        $"Node.js projects: {string.Join(", ", nodeProjects.Select(p => p.Name))}"
                    ]
                };

                patterns.Add(pattern);
            }

            // Check for multi-.NET project pattern
            if (dotNetProjects.Count >= 2)
            {
                bool hasWpf = dotNetProjects.Any(p => 
                    p.Path.Contains("wpf", StringComparison.InvariantCultureIgnoreCase) || 
                    p.Indicators.Any(i => i.Contains("wpf", StringComparison.InvariantCultureIgnoreCase)));

                if (hasWpf)
                {
                    var pattern = new ArchitecturePattern
                    {
                        Type = ArchitectureType.WpfDotNetSharedLibs,
                        Name = "WPF + .NET Libraries",
                        Description = $"WPF application with {dotNetProjects.Count} .NET projects",
                        RootPath = rootPath,
                        ProjectPaths = dotNetProjects.Select(p => p.Path).ToList(),
                        Technologies = ["WPF", ".NET", "C#"],
                        DetectionReasons = [
                            "WPF indicators found",
                            $"{dotNetProjects.Count} .NET projects detected"
                        ]
                    };

                    patterns.Add(pattern);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in combination pattern detection: {ex.Message}");
        }

        return patterns;
    }

    /// <summary>
    /// Detect pattern from a specific combination of project types (used by solution analysis)
    /// </summary>
    public ArchitecturePattern? DetectPatternFromProjectCombination(List<ProjectInfo> projects, string rootPath)
    {
        if (projects.Count == 0) return null;

        List<ProjectType> projectTypes = projects.Select(p => p.Type).ToList();

        // Angular + .NET API pattern (enhanced detection)
        bool hasAngular = projectTypes.Contains(ProjectType.Angular);
        bool hasDotNet = projectTypes.Contains(ProjectType.DotNet);
        
        if (hasAngular && hasDotNet)
        {
            return new ArchitecturePattern
            {
                Type = ArchitectureType.AngularDotNetApi,
                Name = "Angular + .NET API",
                Description = "Angular frontend with .NET backend API",
                RootPath = rootPath,
                ProjectPaths = projects.Select(p => p.Path).ToList(),
                Technologies = ["Angular", ".NET", "C#", "TypeScript"],
                DetectionReasons = [
                    $"Angular project detected: {projects.Where(p => p.Type == ProjectType.Angular).Count()}",
                    $".NET project detected: {projects.Where(p => p.Type == ProjectType.DotNet).Count()}",
                    "Projects in same solution"
                ],
                Indicators = projects.SelectMany(p => p.Indicators.Select(indicator => new PatternIndicator
                {
                    Type = "project_indicator",
                    Value = indicator,
                    Location = p.Path,
                    Weight = 2.0, // Higher weight for solution-based detection
                    Description = $"{p.Type} project indicator"
                })).ToList()
            };
        }

        // Check for WPF + other .NET projects
        List<ProjectInfo> dotNetProjects = projects.Where(p => p.Type == ProjectType.DotNet).ToList();
        if (dotNetProjects.Count >= 2)
        {
            bool hasWpfIndicators = dotNetProjects.Any(p => 
                p.Path.Contains("wpf", StringComparison.InvariantCultureIgnoreCase) || 
                p.Indicators.Any(i => i.Contains("wpf", StringComparison.InvariantCultureIgnoreCase)));

            if (hasWpfIndicators)
            {
                return new ArchitecturePattern
                {
                    Type = ArchitectureType.WpfDotNetSharedLibs,
                    Name = "WPF + .NET Libraries",
                    Description = "WPF application with shared .NET libraries",
                    RootPath = rootPath,
                    ProjectPaths = dotNetProjects.Select(p => p.Path).ToList(),
                    Technologies = ["WPF", ".NET", "C#"],
                    DetectionReasons = [
                        $"{dotNetProjects.Count} .NET projects detected",
                        "WPF indicators found"
                    ]
                };
            }
        }

        // React + Node.js pattern
        if (projectTypes.Contains(ProjectType.React) && projectTypes.Contains(ProjectType.NodeJs))
        {
            return new ArchitecturePattern
            {
                Type = ArchitectureType.ReactNodeJsDatabase,
                Name = "React + Node.js",
                Description = "React frontend with Node.js backend",
                RootPath = rootPath,
                ProjectPaths = projects.Select(p => p.Path).ToList(),
                Technologies = ["React", "Node.js", "JavaScript", "TypeScript"],
                DetectionReasons = ["React project detected", "Node.js project detected", "Projects in same solution"]
            };
        }

        return null;
    }

    /// <summary>
    /// Analyze a solution file for multi-project patterns
    /// </summary>
    private async Task<ArchitecturePattern?> AnalyzeSolutionFileAsync(
        string solutionFile, 
        List<ProjectInfo> availableProjects, 
        CancellationToken cancellationToken)
    {
        try
        {
            string solutionContent = await File.ReadAllTextAsync(solutionFile, cancellationToken);
            var projectsInSolution = new List<ProjectInfo>();

            // Parse solution file to find project references
            string[] lines = solutionContent.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("Project(") && (line.Contains(".csproj") || line.Contains(".esproj")))
                {
                    // Extract project path from solution line
                    string projectPath = ExtractProjectPathFromSolutionLine(line, Path.GetDirectoryName(solutionFile)!);
                    if (!string.IsNullOrEmpty(projectPath))
                    {
                        ProjectInfo? matchingProject = availableProjects.FirstOrDefault(p => 
                            Path.GetDirectoryName(p.Path)?.Equals(Path.GetDirectoryName(projectPath), StringComparison.OrdinalIgnoreCase) == true);
                        
                        if (matchingProject != null)
                        {
                            projectsInSolution.Add(matchingProject);
                        }
                    }
                }
            }

            // Detect pattern based on project types in solution
            return DetectPatternFromProjectCombination(projectsInSolution, Path.GetDirectoryName(solutionFile)!);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing solution file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check if directory has monorepo indicators
    /// </summary>
    private static bool HasMonorepoIndicators(string rootPath)
    {
        var monorepoFiles = new[] { "lerna.json", "nx.json", "rush.json", "pnpm-workspace.yaml", "yarn.lock" };
        return monorepoFiles.Any(file => File.Exists(Path.Combine(rootPath, file)));
    }

    /// <summary>
    /// Extract project path from solution file line
    /// </summary>
    private static string ExtractProjectPathFromSolutionLine(string line, string solutionDir)
    {
        try
        {
            // Parse line like: Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ProjectName", "RelativePath\Project.csproj", "{ProjectGUID}"
            string[] parts = line.Split(',');
            if (parts.Length >= 2)
            {
                // Extract the relative path (second part, between quotes)
                string pathPart = parts[1].Trim();
                if (pathPart.StartsWith("\"") && pathPart.EndsWith("\""))
                {
                    string relativePath = pathPart.Substring(1, pathPart.Length - 2); // Remove quotes
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
