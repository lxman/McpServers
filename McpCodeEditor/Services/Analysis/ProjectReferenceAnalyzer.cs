using McpCodeEditor.Models.Analysis;
using System.Xml.Linq;

namespace McpCodeEditor.Services.Analysis;

/// <summary>
/// Service for analyzing project references and building dependency graphs
/// </summary>
public class ProjectReferenceAnalyzer(ProjectDetectionService projectDetection)
{
    /// <summary>
    /// Analyze all projects in a directory and build a complete dependency graph
    /// </summary>
    public async Task<ProjectDependencyGraph> AnalyzeDirectoryAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var graph = new ProjectDependencyGraph
        {
            RootPath = rootPath,
            AnalysisDate = DateTime.UtcNow
        };

        try
        {
            // Step 1: Discover all project files
            List<string> projectFiles = await DiscoverProjectFilesAsync(rootPath, cancellationToken);

            // Step 2: Create project nodes
            foreach (string projectFile in projectFiles)
            {
                ProjectNode? projectNode = await CreateProjectNodeAsync(projectFile, cancellationToken);
                if (projectNode != null)
                {
                    graph.Projects.Add(projectNode);
                }
            }

            // Step 3: Analyze references for each project
            foreach (ProjectNode project in graph.Projects)
            {
                List<ProjectReference> references = await AnalyzeProjectReferencesAsync(project.ProjectPath, rootPath, cancellationToken);
                graph.References.AddRange(references);
            }

            // Step 4: Calculate statistics
            graph.Statistics = CalculateStatistics(graph);

            return graph;
        }
        catch (Exception)
        {
            // Return partial results if analysis fails
            return graph;
        }
    }

    /// <summary>
    /// Discover all project files in a directory tree
    /// </summary>
    private static async Task<List<string>> DiscoverProjectFilesAsync(string rootPath, CancellationToken cancellationToken)
    {
        var projectFiles = new List<string>();

        try
        {
            // Look for .csproj files (C# projects)
            string[] csprojFiles = Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories);
            projectFiles.AddRange(csprojFiles);

            // Look for .esproj files (JavaScript/Node.js projects in .NET solutions)
            string[] esprojFiles = Directory.GetFiles(rootPath, "*.esproj", SearchOption.AllDirectories);
            projectFiles.AddRange(esprojFiles);

            // Look for .vbproj files (VB.NET projects)
            string[] vbprojFiles = Directory.GetFiles(rootPath, "*.vbproj", SearchOption.AllDirectories);
            projectFiles.AddRange(vbprojFiles);

            // Look for .fsproj files (F# projects)
            string[] fsprojFiles = Directory.GetFiles(rootPath, "*.fsproj", SearchOption.AllDirectories);
            projectFiles.AddRange(fsprojFiles);

            // Filter out excluded directories
            projectFiles = projectFiles.Where(file => !IsInExcludedDirectory(file)).ToList();
        }
        catch (Exception)
        {
            // Continue with empty list if discovery fails
        }

        return projectFiles;
    }

    /// <summary>
    /// Create a project node from a project file
    /// </summary>
    private async Task<ProjectNode?> CreateProjectNodeAsync(string projectPath, CancellationToken cancellationToken)
    {
        try
        {
            string projectName = Path.GetFileNameWithoutExtension(projectPath);
            string? projectDirectory = Path.GetDirectoryName(projectPath);

            if (string.IsNullOrEmpty(projectDirectory))
                return null;

            // Use existing project detection to classify the project
            ProjectInfo projectInfo = await projectDetection.AnalyzeDirectoryAsync(projectDirectory);

            var node = new ProjectNode
            {
                ProjectPath = projectPath,
                ProjectName = projectName,
                ProjectType = projectInfo.Type,
                PlatformIndicators = projectInfo.Indicators.ToList(),
                LastAnalyzed = DateTime.UtcNow
            };

            // Try to extract target frameworks from project file
            node.TargetFrameworks = await ExtractTargetFrameworksAsync(projectPath, cancellationToken);

            return node;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Analyze all references in a project file
    /// </summary>
    private static async Task<List<ProjectReference>> AnalyzeProjectReferencesAsync(string projectPath, string rootPath, CancellationToken cancellationToken)
    {
        var references = new List<ProjectReference>();

        try
        {
            string projectContent = await File.ReadAllTextAsync(projectPath, cancellationToken);

            // Parse as XML to extract references
            XDocument doc = XDocument.Parse(projectContent);

            // Extract ProjectReferences
            List<ProjectReference> projectRefs = ExtractProjectReferences(doc, projectPath, rootPath);
            references.AddRange(projectRefs);

            // Extract PackageReferences
            List<ProjectReference> packageRefs = ExtractPackageReferences(doc, projectPath);
            references.AddRange(packageRefs);

            // Extract legacy References (for older project formats)
            List<ProjectReference> legacyRefs = ExtractLegacyReferences(doc, projectPath);
            references.AddRange(legacyRefs);
        }
        catch (Exception)
        {
            // Continue with empty references if parsing fails
        }

        return references;
    }

    /// <summary>
    /// Extract ProjectReference elements from project XML
    /// </summary>
    private static List<ProjectReference> ExtractProjectReferences(XDocument doc, string sourceProjectPath, string rootPath)
    {
        var references = new List<ProjectReference>();

        try
        {
            List<XElement> projectRefs = doc.Descendants("ProjectReference").ToList();

            foreach (XElement projectRef in projectRefs)
            {
                string? includeAttr = projectRef.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(includeAttr))
                    continue;

                // Resolve relative path to absolute path
                string sourceDir = Path.GetDirectoryName(sourceProjectPath)!;
                string targetPath = Path.GetFullPath(Path.Combine(sourceDir, includeAttr));

                var reference = new ProjectReference
                {
                    Type = ProjectReferenceType.Project,
                    SourceProjectPath = sourceProjectPath,
                    TargetPath = targetPath,
                    OriginalReference = includeAttr,
                    ConfidenceScore = 1.0 // High confidence for explicit project references
                };

                references.Add(reference);
            }
        }
        catch (Exception)
        {
            // Continue with partial results
        }

        return references;
    }

    /// <summary>
    /// Extract PackageReference elements from project XML
    /// </summary>
    private static List<ProjectReference> ExtractPackageReferences(XDocument doc, string sourceProjectPath)
    {
        var references = new List<ProjectReference>();

        try
        {
            List<XElement> packageRefs = doc.Descendants("PackageReference").ToList();

            foreach (XElement packageRef in packageRefs)
            {
                string? includeAttr = packageRef.Attribute("Include")?.Value;
                string? versionAttr = packageRef.Attribute("Version")?.Value;
                
                if (string.IsNullOrEmpty(includeAttr))
                    continue;

                var reference = new ProjectReference
                {
                    Type = ProjectReferenceType.Package,
                    SourceProjectPath = sourceProjectPath,
                    TargetPath = includeAttr,
                    OriginalReference = includeAttr,
                    Version = versionAttr,
                    ConfidenceScore = 1.0
                };

                // Check for development dependencies
                string? privateAssetsAttr = packageRef.Attribute("PrivateAssets")?.Value;
                if (privateAssetsAttr?.Contains("all", StringComparison.OrdinalIgnoreCase) == true)
                {
                    reference.IsDevDependency = true;
                }

                references.Add(reference);
            }
        }
        catch (Exception)
        {
            // Continue with partial results
        }

        return references;
    }

    /// <summary>
    /// Extract legacy Reference elements (for older project formats)
    /// </summary>
    private static List<ProjectReference> ExtractLegacyReferences(XDocument doc, string sourceProjectPath)
    {
        var references = new List<ProjectReference>();

        try
        {
            List<XElement> legacyRefs = doc.Descendants("Reference").ToList();

            foreach (XElement legacyRef in legacyRefs)
            {
                string? includeAttr = legacyRef.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(includeAttr))
                    continue;

                // Determine if it's a framework or assembly reference
                ProjectReferenceType referenceType = DetermineReferenceType(includeAttr);

                var reference = new ProjectReference
                {
                    Type = referenceType,
                    SourceProjectPath = sourceProjectPath,
                    TargetPath = includeAttr,
                    OriginalReference = includeAttr,
                    ConfidenceScore = 0.8 // Lower confidence for legacy references
                };

                references.Add(reference);
            }
        }
        catch (Exception)
        {
            // Continue with partial results
        }

        return references;
    }

    /// <summary>
    /// Extract target frameworks from project file
    /// </summary>
    private static async Task<List<string>> ExtractTargetFrameworksAsync(string projectPath, CancellationToken cancellationToken)
    {
        var frameworks = new List<string>();

        try
        {
            string projectContent = await File.ReadAllTextAsync(projectPath, cancellationToken);
            XDocument doc = XDocument.Parse(projectContent);

            // Look for TargetFramework (single)
            string? targetFramework = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(targetFramework))
            {
                frameworks.Add(targetFramework);
            }

            // Look for TargetFrameworks (multiple)
            string? targetFrameworks = doc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                string[] multipleFrameworks = targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries);
                frameworks.AddRange(multipleFrameworks.Select(f => f.Trim()));
            }
        }
        catch (Exception)
        {
            // Continue with empty frameworks
        }

        return frameworks;
    }

    /// <summary>
    /// Determine the type of a legacy reference
    /// </summary>
    private static ProjectReferenceType DetermineReferenceType(string reference)
    {
        // System assemblies
        if (reference.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
            reference.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            reference.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectReferenceType.Framework;
        }

        // Everything else is considered an assembly reference
        return ProjectReferenceType.Assembly;
    }

    /// <summary>
    /// Check if a file is in an excluded directory
    /// </summary>
    private static bool IsInExcludedDirectory(string filePath)
    {
        var excludedDirs = new[] { "bin", "obj", "packages", "node_modules", ".git", ".vs", ".vscode" };
        string[] pathSegments = filePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        
        return excludedDirs.Any(excluded => 
            pathSegments.Any(segment => segment.Equals(excluded, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Calculate statistics for the dependency graph
    /// </summary>
    private static GraphStatistics CalculateStatistics(ProjectDependencyGraph graph)
    {
        var stats = new GraphStatistics
        {
            TotalProjects = graph.Projects.Count,
            TotalReferences = graph.References.Count,
            ProjectReferences = graph.References.Count(r => r.Type == ProjectReferenceType.Project),
            PackageReferences = graph.References.Count(r => r.Type == ProjectReferenceType.Package),
            CircularDependencies = graph.DetectCircularDependencies().Count
        };

        // Calculate max dependency depth
        stats.MaxDependencyDepth = CalculateMaxDependencyDepth(graph);

        // Calculate overall confidence score
        if (graph.References.Count != 0)
        {
            stats.ConfidenceScore = graph.References.Average(r => r.ConfidenceScore);
        }

        return stats;
    }

    /// <summary>
    /// Calculate the maximum dependency depth in the graph
    /// </summary>
    private static int CalculateMaxDependencyDepth(ProjectDependencyGraph graph)
    {
        var maxDepth = 0;

        foreach (ProjectNode project in graph.Projects)
        {
            int depth = CalculateProjectDepth(graph, project.ProjectPath, []);
            maxDepth = Math.Max(maxDepth, depth);
        }

        return maxDepth;
    }

    /// <summary>
    /// Calculate dependency depth for a specific project
    /// </summary>
    private static int CalculateProjectDepth(ProjectDependencyGraph graph, string projectPath, HashSet<string> visited)
    {
        if (visited.Contains(projectPath))
            return 0; // Circular dependency protection

        visited.Add(projectPath);

        List<ProjectNode> dependencies = graph.GetDependencies(projectPath);
        if (dependencies.Count == 0)
            return 1; // Leaf node

        var maxChildDepth = 0;
        foreach (ProjectNode dependency in dependencies)
        {
            int childDepth = CalculateProjectDepth(graph, dependency.ProjectPath, [..visited]);
            maxChildDepth = Math.Max(maxChildDepth, childDepth);
        }

        return maxChildDepth + 1;
    }
}
