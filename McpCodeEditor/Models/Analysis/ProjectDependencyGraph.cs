namespace McpCodeEditor.Models.Analysis;

/// <summary>
/// Represents the complete dependency graph of projects in a solution or directory
/// </summary>
public class ProjectDependencyGraph
{
    /// <summary>
    /// All projects discovered in the analysis
    /// </summary>
    public List<ProjectNode> Projects { get; set; } = [];

    /// <summary>
    /// All references between projects and packages
    /// </summary>
    public List<ProjectReference> References { get; set; } = [];

    /// <summary>
    /// Root path of the analysis
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// When this graph was created
    /// </summary>
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Analysis statistics
    /// </summary>
    public GraphStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Get all projects that a specific project depends on
    /// </summary>
    public List<ProjectNode> GetDependencies(string projectPath)
    {
        var dependencies = new List<ProjectNode>();
        List<ProjectReference> projectReferences = References
            .Where(r => r.SourceProjectPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
            .Where(r => r.Type == ProjectReferenceType.Project)
            .ToList();

        foreach (ProjectReference reference in projectReferences)
        {
            ProjectNode? dependentProject = Projects.FirstOrDefault(p =>
                p.ProjectPath.Equals(reference.TargetPath, StringComparison.OrdinalIgnoreCase));

            if (dependentProject != null)
            {
                dependencies.Add(dependentProject);
            }
        }

        return dependencies;
    }

    /// <summary>
    /// Get all projects that depend on a specific project
    /// </summary>
    public List<ProjectNode> GetDependents(string projectPath)
    {
        var dependents = new List<ProjectNode>();
        List<ProjectReference> projectReferences = References
            .Where(r => r.TargetPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
            .Where(r => r.Type == ProjectReferenceType.Project)
            .ToList();

        foreach (ProjectReference reference in projectReferences)
        {
            ProjectNode? dependentProject = Projects.FirstOrDefault(p =>
                p.ProjectPath.Equals(reference.SourceProjectPath, StringComparison.OrdinalIgnoreCase));

            if (dependentProject != null)
            {
                dependents.Add(dependentProject);
            }
        }

        return dependents;
    }

    /// <summary>
    /// Check if there's a dependency path between two projects
    /// </summary>
    public bool HasDependencyPath(string fromProject, string toProject)
    {
        var visited = new HashSet<string>();
        return HasDependencyPathRecursive(fromProject, toProject, visited);
    }

    private bool HasDependencyPathRecursive(string current, string target, HashSet<string> visited)
    {
        if (current.Equals(target, StringComparison.OrdinalIgnoreCase))
            return true;

        if (visited.Contains(current))
            return false; // Circular dependency protection

        visited.Add(current);

        List<ProjectNode> dependencies = GetDependencies(current);
        foreach (ProjectNode dependency in dependencies)
        {
            if (HasDependencyPathRecursive(dependency.ProjectPath, target, visited))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Detect circular dependencies in the graph
    /// </summary>
    public List<List<string>> DetectCircularDependencies()
    {
        var cycles = new List<List<string>>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (ProjectNode project in Projects)
        {
            if (!visited.Contains(project.ProjectPath))
            {
                var path = new List<string>();
                FindCircularDependencies(project.ProjectPath, visited, recursionStack, path, cycles);
            }
        }

        return cycles;
    }

    private void FindCircularDependencies(string projectPath, HashSet<string> visited,
        HashSet<string> recursionStack, List<string> currentPath, List<List<string>> cycles)
    {
        visited.Add(projectPath);
        recursionStack.Add(projectPath);
        currentPath.Add(projectPath);

        List<ProjectNode> dependencies = GetDependencies(projectPath);
        foreach (ProjectNode dependency in dependencies)
        {
            if (!visited.Contains(dependency.ProjectPath))
            {
                FindCircularDependencies(dependency.ProjectPath, visited, recursionStack, currentPath, cycles);
            }
            else if (recursionStack.Contains(dependency.ProjectPath))
            {
                // Found a cycle
                int cycleStart = currentPath.IndexOf(dependency.ProjectPath);
                if (cycleStart >= 0)
                {
                    List<string> cycle = currentPath.Skip(cycleStart).ToList();
                    cycle.Add(dependency.ProjectPath); // Complete the cycle
                    cycles.Add(cycle);
                }
            }
        }

        recursionStack.Remove(projectPath);
        currentPath.RemoveAt(currentPath.Count - 1);
    }

    /// <summary>
    /// Get projects that have no dependencies (leaf nodes)
    /// </summary>
    public List<ProjectNode> GetLeafProjects()
    {
        return Projects.Where(p => GetDependencies(p.ProjectPath).Count == 0).ToList();
    }

    /// <summary>
    /// Get projects that nothing depends on (root nodes)
    /// </summary>
    public List<ProjectNode> GetRootProjects()
    {
        return Projects.Where(p => GetDependents(p.ProjectPath).Count == 0).ToList();
    }
}
