using System.Xml.Linq;
using CodeAssist.Core.Models.Graph;
using Microsoft.Extensions.Logging;

namespace CodeAssist.Core.Analysis;

/// <summary>
/// Parses .slnx/.sln and .csproj files to extract the project-level structure
/// of a solution: projects, solution folders, project references, and package references.
/// Uses lightweight XML parsing — does not require MSBuild evaluation.
/// </summary>
public sealed class SolutionAnalyzer(ILogger<SolutionAnalyzer> logger)
{
    /// <summary>
    /// Analyze a solution file and all its projects.
    /// </summary>
    public SolutionStructure? Analyze(string solutionPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(solutionPath);
            string solutionDir = Path.GetDirectoryName(fullPath)!;
            string solutionName = Path.GetFileNameWithoutExtension(fullPath);

            if (fullPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                return ParseSlnx(fullPath, solutionDir, solutionName);

            logger.LogWarning("Only .slnx format is supported. Got: {Path}", solutionPath);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to analyze solution {Path}", solutionPath);
            return null;
        }
    }

    /// <summary>
    /// Find the solution file in a repository directory.
    /// </summary>
    public static string? FindSolutionFile(string repositoryPath)
    {
        // Prefer .slnx over .sln
        string[] slnxFiles = Directory.GetFiles(repositoryPath, "*.slnx", SearchOption.TopDirectoryOnly);
        if (slnxFiles.Length == 1)
            return slnxFiles[0];

        string[] slnFiles = Directory.GetFiles(repositoryPath, "*.sln", SearchOption.TopDirectoryOnly);
        if (slnFiles.Length == 1)
            return slnFiles[0];

        return null;
    }

    /// <summary>
    /// Analyze a repository that has no solution file by scanning for .csproj files directly.
    /// Builds a synthetic SolutionStructure from whatever projects are found.
    /// </summary>
    public SolutionStructure? AnalyzeFromCsprojFiles(string repositoryPath)
    {
        try
        {
            string fullPath = Path.GetFullPath(repositoryPath);
            string repoName = Path.GetFileName(fullPath);

            string[] csprojFiles = Directory.GetFiles(fullPath, "*.csproj", SearchOption.AllDirectories);
            if (csprojFiles.Length == 0)
            {
                logger.LogDebug("No .csproj files found in {Path}", repositoryPath);
                return null;
            }

            var projects = new List<ProjectInfo>();
            foreach (string csprojPath in csprojFiles)
            {
                string relativePath = Path.GetRelativePath(fullPath, csprojPath).Replace('\\', '/');
                ProjectInfo? project = ParseCsproj(relativePath, fullPath, solutionFolder: null);
                if (project != null)
                    projects.Add(project);
            }

            if (projects.Count == 0) return null;

            logger.LogInformation(
                "Built structure from .csproj scan in {Name}: {ProjectCount} projects",
                repoName, projects.Count);

            return new SolutionStructure
            {
                Name = repoName,
                FilePath = fullPath,
                Projects = projects,
                Folders = []
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan .csproj files in {Path}", repositoryPath);
            return null;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  .slnx Parsing
    // ────────────────────────────────────────────────────────────────

    private SolutionStructure ParseSlnx(string slnxPath, string solutionDir, string solutionName)
    {
        XDocument doc = XDocument.Load(slnxPath);
        XElement root = doc.Root ?? throw new InvalidOperationException("Empty .slnx file");

        var projects = new List<ProjectInfo>();
        var folders = new List<SolutionFolderInfo>();

        // Parse top-level projects (no folder)
        foreach (XElement projectEl in root.Elements("Project"))
        {
            string? path = projectEl.Attribute("Path")?.Value;
            if (string.IsNullOrEmpty(path)) continue;

            ProjectInfo? project = ParseCsproj(path, solutionDir, solutionFolder: null);
            if (project != null)
                projects.Add(project);
        }

        // Parse folder-grouped projects
        foreach (XElement folderEl in root.Elements("Folder"))
        {
            string folderName = folderEl.Attribute("Name")?.Value?.Trim('/') ?? "Unknown";
            var folderProjectNames = new List<string>();

            foreach (XElement projectEl in folderEl.Elements("Project"))
            {
                string? path = projectEl.Attribute("Path")?.Value;
                if (string.IsNullOrEmpty(path)) continue;

                ProjectInfo? project = ParseCsproj(path, solutionDir, folderName);
                if (project != null)
                {
                    projects.Add(project);
                    folderProjectNames.Add(project.Name);
                }
            }

            if (folderProjectNames.Count > 0)
            {
                folders.Add(new SolutionFolderInfo
                {
                    Name = folderName,
                    ProjectNames = folderProjectNames
                });
            }
        }

        logger.LogInformation(
            "Parsed solution {Name}: {ProjectCount} projects, {FolderCount} folders",
            solutionName, projects.Count, folders.Count);

        return new SolutionStructure
        {
            Name = solutionName,
            FilePath = slnxPath,
            Projects = projects,
            Folders = folders
        };
    }

    // ────────────────────────────────────────────────────────────────
    //  .csproj Parsing
    // ────────────────────────────────────────────────────────────────

    private ProjectInfo? ParseCsproj(string relativeCsprojPath, string solutionDir, string? solutionFolder)
    {
        // Normalize path separators
        string normalized = relativeCsprojPath.Replace('\\', '/');
        string absolutePath = Path.GetFullPath(Path.Combine(solutionDir, normalized));

        if (!File.Exists(absolutePath))
        {
            logger.LogWarning("Project file not found: {Path}", absolutePath);
            return null;
        }

        try
        {
            XDocument doc = XDocument.Load(absolutePath);
            XElement root = doc.Root ?? throw new InvalidOperationException("Empty .csproj file");
            string projectName = Path.GetFileNameWithoutExtension(absolutePath);
            string projectDir = Path.GetDirectoryName(absolutePath)!;

            // Extract properties from the first PropertyGroup
            string? targetFramework = null;
            string? outputType = null;

            foreach (XElement propGroup in root.Elements("PropertyGroup"))
            {
                targetFramework ??= propGroup.Element("TargetFramework")?.Value
                                 ?? propGroup.Element("TargetFrameworks")?.Value;
                outputType ??= propGroup.Element("OutputType")?.Value;
            }

            // Extract ProjectReference entries
            var projectRefs = new List<string>();
            foreach (XElement itemGroup in root.Elements("ItemGroup"))
            {
                foreach (XElement projRef in itemGroup.Elements("ProjectReference"))
                {
                    string? include = projRef.Attribute("Include")?.Value;
                    if (!string.IsNullOrEmpty(include))
                    {
                        string refName = Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));
                        projectRefs.Add(refName);
                    }
                }
            }

            // Extract PackageReference entries
            var packageRefs = new List<PackageRef>();
            foreach (XElement itemGroup in root.Elements("ItemGroup"))
            {
                foreach (XElement pkgRef in itemGroup.Elements("PackageReference"))
                {
                    string? name = pkgRef.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;

                    string? version = pkgRef.Attribute("Version")?.Value
                                  ?? pkgRef.Element("Version")?.Value;

                    packageRefs.Add(new PackageRef { Name = name, Version = version });
                }
            }

            return new ProjectInfo
            {
                Name = projectName,
                RelativePath = normalized,
                SolutionFolder = solutionFolder,
                TargetFramework = targetFramework,
                OutputType = outputType,
                ProjectReferences = projectRefs,
                PackageReferences = packageRefs
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse project file {Path}", absolutePath);
            return null;
        }
    }
}
