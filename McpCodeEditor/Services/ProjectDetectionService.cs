namespace McpCodeEditor.Services;

public enum ProjectType
{
    Unknown,
    DotNet,
    NodeJs,
    Python,
    Java,
    React,
    Angular,
    Git,
    Mixed
}

public class ProjectInfo
{
    public string Path { get; set; } = string.Empty;
    public ProjectType Type { get; set; } = ProjectType.Unknown;
    public string Name { get; set; } = string.Empty;
    public List<string> Indicators { get; set; } = [];
    public int Score { get; set; } = 0;
    public string Description { get; set; } = string.Empty;
}

public class ProjectDetectionService
{
    private readonly Dictionary<ProjectType, string[]> _projectPatterns = new()
    {
        [ProjectType.DotNet] = ["*.sln", "*.csproj", "*.vbproj", "*.fsproj", "global.json"],
        [ProjectType.NodeJs] = ["package.json", "yarn.lock", "pnpm-lock.yaml", "node_modules"],
        [ProjectType.Python] = ["requirements.txt", "setup.py", "pyproject.toml", "Pipfile", "__pycache__"],
        [ProjectType.Java] = ["pom.xml", "build.gradle", "gradlew", "build.xml", "target"],
        [ProjectType.React] = ["package.json"], // Special case - requires package.json analysis
        [ProjectType.Angular] = ["angular.json", "ng-cli.json", ".angular-cli.json"],
        [ProjectType.Git] = [".git"]
    };

    /// <summary>
    /// Detects the best workspace directory by searching common development locations
    /// </summary>
    public async Task<List<ProjectInfo>> DetectWorkspacesAsync()
    {
        var projects = new List<ProjectInfo>();

        // Common development directories
        string[] searchPaths =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), // C:\Users\username
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "repos"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "dev"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "projects"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RiderProjects"), // Your specific case
            "C:\\dev",
            "C:\\source",
            "C:\\projects",
            Environment.CurrentDirectory // Fallback to current directory
        ];

        // Search each path for projects
        foreach (var searchPath in searchPaths.Where(Directory.Exists))
        {
            try
            {
                // Search immediate subdirectories for project patterns
                var subdirs = Directory.GetDirectories(searchPath, "*", SearchOption.TopDirectoryOnly)
                    .Take(50) // Limit to avoid performance issues
                    .Where(dir => !IsSystemDirectory(dir));

                foreach (var dir in subdirs)
                {
                    var projectInfo = await AnalyzeDirectoryAsync(dir);
                    if (projectInfo.Type != ProjectType.Unknown)
                    {
                        projects.Add(projectInfo);
                    }
                }

                // Also check the search path itself
                var rootProject = await AnalyzeDirectoryAsync(searchPath);
                if (rootProject.Type != ProjectType.Unknown)
                {
                    projects.Add(rootProject);
                }
            }
            catch
            {
                // Skip directories we can't access
                continue;
            }
        }

        // Sort by score (most relevant projects first)
        return projects.OrderByDescending(p => p.Score).ToList();
    }

    /// <summary>
    /// Analyzes a directory to determine if it's a project and what type
    /// </summary>
    public async Task<ProjectInfo> AnalyzeDirectoryAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            return new ProjectInfo { Path = path };
        }

        var projectInfo = new ProjectInfo
        {
            Path = path,
            Name = Path.GetFileName(path)
        };

        var files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
        var directories = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
        var allItems = files.Concat(directories).Select(Path.GetFileName).ToArray();

        // Check each project type
        foreach ((var type, var patterns) in _projectPatterns)
        {
            var matches = patterns.Where(pattern =>
                allItems.Any(item => MatchesPattern(item, pattern))).ToList();

            if (matches.Count != 0)
            {
                projectInfo.Type = type;
                projectInfo.Indicators.AddRange(matches);
                projectInfo.Score += matches.Count * 10; // Base score for matching patterns
            }
        }

        // Special handling for React projects (check package.json content)
        if (projectInfo.Type == ProjectType.NodeJs)
        {
            var packageJsonPath = Path.Combine(path, "package.json");
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    var packageContent = await File.ReadAllTextAsync(packageJsonPath);
                    if (packageContent.Contains("\"react\"") || packageContent.Contains("\"@types/react\""))
                    {
                        projectInfo.Type = ProjectType.React;
                        projectInfo.Indicators.Add("React dependencies in package.json");
                        projectInfo.Score += 15; // Bonus for React
                    }
                    else if (packageContent.Contains("\"@angular\""))
                    {
                        projectInfo.Type = ProjectType.Angular;
                        projectInfo.Indicators.Add("Angular dependencies in package.json");
                        projectInfo.Score += 15; // Bonus for Angular
                    }
                }
                catch
                {
                    // Ignore JSON parsing errors
                }
            }
        }

        // Bonus scoring based on recency and size
        if (projectInfo.Type != ProjectType.Unknown)
        {
            // Bonus for recently modified directories
            var lastWrite = Directory.GetLastWriteTime(path);
            if (lastWrite > DateTime.Now.AddDays(-30))
            {
                projectInfo.Score += 20; // Recent activity bonus
            }
            else if (lastWrite > DateTime.Now.AddDays(-90))
            {
                projectInfo.Score += 10; // Somewhat recent bonus
            }

            // Bonus for larger projects (more likely to be active)
            var fileCount = files.Length;
            if (fileCount > 50)
            {
                projectInfo.Score += 15; // Large project bonus
            }
            else if (fileCount > 10)
            {
                projectInfo.Score += 5; // Medium project bonus
            }

            // Generate description
            projectInfo.Description = GenerateDescription(projectInfo);
        }

        return projectInfo;
    }

    /// <summary>
    /// Suggests the best workspace based on the current context
    /// </summary>
    public async Task<ProjectInfo?> SuggestBestWorkspaceAsync()
    {
        var projects = await DetectWorkspacesAsync();

        // Prefer the highest-scored project that's not a system directory
        return projects
            .FirstOrDefault(p => !IsSystemDirectory(p.Path));
    }

    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern.Contains('*'))
        {
            // Simple glob pattern matching
            var regex = "^" + pattern.Replace("*", ".*").Replace("?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(fileName, regex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSystemDirectory(string path)
    {
        var systemPaths = new[]
        {
            "C:\\Windows", "C:\\Program Files", "C:\\Program Files (x86)",
            "/usr", "/bin", "/sbin", "/etc", "/var", "/sys", "/proc"
        };

        return systemPaths.Any(sysPath =>
            path.StartsWith(sysPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateDescription(ProjectInfo project)
    {
        var desc = project.Type switch
        {
            ProjectType.DotNet => "C#/.NET project",
            ProjectType.NodeJs => "Node.js project",
            ProjectType.Python => "Python project",
            ProjectType.Java => "Java project",
            ProjectType.React => "React application",
            ProjectType.Angular => "Angular application",
            ProjectType.Git => "Git repository",
            _ => "Unknown project type"
        };

        if (project.Indicators.Count != 0)
        {
            desc += $" ({string.Join(", ", project.Indicators.Take(3))})";
        }

        return desc;
    }
}
