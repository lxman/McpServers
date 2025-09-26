using Microsoft.Extensions.Logging;

namespace McpCodeEditor.Services.TypeScript;

/// <summary>
/// Service for resolving TypeScript file paths and discovering TypeScript projects
/// Handles cross-project analysis and intelligent path resolution
/// </summary>
public class TypeScriptFileResolver(ILogger<TypeScriptFileResolver> logger)
{
    private readonly string[] _typescriptExtensions = [".ts", ".tsx"];
    private readonly string[] _commonExcludedDirs = ["node_modules", "dist", "build", ".git", ".vs", "bin", "obj"];
    
    /// <summary>
    /// Find TypeScript files in a specified directory with intelligent filtering
    /// </summary>
    public async Task<TypeScriptFileDiscoveryResult> FindTypeScriptFilesAsync(
        string searchPath, 
        bool includeNodeModules = false, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(searchPath))
            {
                return new TypeScriptFileDiscoveryResult
                {
                    Success = false,
                    ErrorMessage = $"Directory not found: {searchPath}"
                };
            }

            var result = new TypeScriptFileDiscoveryResult
            {
                Success = true,
                SearchPath = searchPath,
                Timestamp = DateTime.UtcNow
            };

            await Task.Run(() =>
            {
                // Get all TypeScript files
                var allFiles = new List<string>();
                
                foreach (string extension in _typescriptExtensions)
                {
                    var pattern = $"*{extension}";
                    string[] files = Directory.GetFiles(searchPath, pattern, SearchOption.AllDirectories);
                    allFiles.AddRange(files);
                }

                // Filter excluded directories
                string[] excludedDirs = includeNodeModules 
                    ? _commonExcludedDirs.Where(d => d != "node_modules").ToArray()
                    : _commonExcludedDirs;

                result.AllFiles = allFiles
                    .Where(file => !IsInExcludedDirectory(file, excludedDirs))
                    .OrderBy(f => f)
                    .ToList();

                // Categorize files
                result.SourceFiles = result.AllFiles
                    .Where(f => !IsTestFile(f) && !IsDeclarationFile(f))
                    .ToList();
                    
                result.TestFiles = result.AllFiles
                    .Where(IsTestFile)
                    .ToList();
                    
                result.DeclarationFiles = result.AllFiles
                    .Where(IsDeclarationFile)
                    .ToList();

                cancellationToken.ThrowIfCancellationRequested();
            }, cancellationToken);

            logger.LogDebug("Found {TotalFiles} TypeScript files in {SearchPath}: {SourceFiles} source, {TestFiles} test, {DeclarationFiles} declaration",
                result.AllFiles.Count, searchPath, result.SourceFiles.Count, result.TestFiles.Count, result.DeclarationFiles.Count);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error discovering TypeScript files in {SearchPath}", searchPath);
            return new TypeScriptFileDiscoveryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SearchPath = searchPath
            };
        }
    }

    /// <summary>
    /// Attempt to discover TypeScript projects in common locations
    /// </summary>
    public async Task<List<TypeScriptProjectInfo>> DiscoverTypeScriptProjectsAsync(
        string? baseSearchPath = null, 
        CancellationToken cancellationToken = default)
    {
        var projects = new List<TypeScriptProjectInfo>();
        
        try
        {
            // Default search locations if no base path provided
            var searchPaths = new List<string>();
            
            if (!string.IsNullOrEmpty(baseSearchPath) && Directory.Exists(baseSearchPath))
            {
                searchPaths.Add(baseSearchPath);
            }
            else
            {
                // Search common development locations
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                string[] commonPaths =
                [
                    Path.Combine(userProfile, "RiderProjects"),
                    Path.Combine(userProfile, "source", "repos"),
                    Path.Combine(userProfile, "Documents", "Projects"),
                    Path.Combine(userProfile, "Projects"),
                    @"C:\Dev",
                    @"C:\Source"
                ];

                searchPaths.AddRange(commonPaths.Where(Directory.Exists));
            }

            foreach (string searchPath in searchPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    // Look for directories that contain TypeScript config files
                    string[] tsConfigFiles = Directory.GetFiles(searchPath, "tsconfig.json", SearchOption.AllDirectories);
                    string[] packageJsonFiles = Directory.GetFiles(searchPath, "package.json", SearchOption.AllDirectories);
                    
                    List<string?> projectDirs = tsConfigFiles.Select(Path.GetDirectoryName)
                        .Union(packageJsonFiles.Select(Path.GetDirectoryName))
                        .Where(dir => !string.IsNullOrEmpty(dir))
                        .Where(dir => !IsInExcludedDirectory(dir!, _commonExcludedDirs))
                        .Distinct()
                        .ToList();

                    foreach (string? projectDir in projectDirs)
                    {
                        if (projectDir == null) continue;
                        
                        TypeScriptFileDiscoveryResult fileResult = await FindTypeScriptFilesAsync(projectDir, false, cancellationToken);
                        
                        if (fileResult is { Success: true, SourceFiles.Count: > 0 })
                        {
                            var projectInfo = new TypeScriptProjectInfo
                            {
                                ProjectPath = projectDir,
                                ProjectName = Path.GetFileName(projectDir),
                                HasTsConfig = File.Exists(Path.Combine(projectDir, "tsconfig.json")),
                                HasPackageJson = File.Exists(Path.Combine(projectDir, "package.json")),
                                SourceFileCount = fileResult.SourceFiles.Count,
                                TestFileCount = fileResult.TestFiles.Count,
                                DeclarationFileCount = fileResult.DeclarationFiles.Count,
                                IsAngularProject = IsAngularProject(projectDir),
                                IsReactProject = IsReactProject(projectDir),
                                DiscoveredAt = DateTime.UtcNow
                            };
                            
                            projects.Add(projectInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error searching for TypeScript projects in {SearchPath}", searchPath);
                    // Continue with other search paths
                }
            }

            logger.LogInformation("Discovered {ProjectCount} TypeScript projects", projects.Count);
            return projects;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during TypeScript project discovery");
            return projects;
        }
    }

    /// <summary>
    /// Resolve a file path, handling both absolute and relative paths
    /// </summary>
    public string ResolvePath(string filePath, string? basePath = null)
    {
        try
        {
            if (Path.IsPathRooted(filePath))
            {
                return Path.GetFullPath(filePath);
            }

            if (!string.IsNullOrEmpty(basePath))
            {
                return Path.GetFullPath(Path.Combine(basePath, filePath));
            }

            return Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error resolving path: {FilePath} with base: {BasePath}", filePath, basePath);
            return filePath;
        }
    }

    /// <summary>
    /// Check if a file exists and is a TypeScript file
    /// </summary>
    public bool IsValidTypeScriptFile(string filePath)
    {
        try
        {
            return File.Exists(filePath) && _typescriptExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    #region Private Helper Methods

    private static bool IsInExcludedDirectory(string filePath, string[] excludedDirs)
    {
        string[] pathParts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return pathParts.Any(part => excludedDirs.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsTestFile(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
        return fileName.Contains("test") || fileName.Contains("spec") || 
               filePath.Contains($"{Path.DirectorySeparatorChar}test{Path.DirectorySeparatorChar}") ||
               filePath.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}") ||
               filePath.Contains($"{Path.DirectorySeparatorChar}__tests__{Path.DirectorySeparatorChar}");
    }

    private static bool IsDeclarationFile(string filePath)
    {
        return filePath.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAngularProject(string projectPath)
    {
        try
        {
            string packageJsonPath = Path.Combine(projectPath, "package.json");
            if (!File.Exists(packageJsonPath)) return false;

            string packageContent = File.ReadAllText(packageJsonPath);
            return packageContent.Contains("\"@angular/core\"", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReactProject(string projectPath)
    {
        try
        {
            string packageJsonPath = Path.Combine(projectPath, "package.json");
            if (!File.Exists(packageJsonPath)) return false;

            string packageContent = File.ReadAllText(packageJsonPath);
            return packageContent.Contains("\"react\"", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

/// <summary>
/// Result of TypeScript file discovery operation
/// </summary>
public class TypeScriptFileDiscoveryResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string SearchPath { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    
    public List<string> AllFiles { get; set; } = [];
    public List<string> SourceFiles { get; set; } = [];
    public List<string> TestFiles { get; set; } = [];
    public List<string> DeclarationFiles { get; set; } = [];
}

/// <summary>
/// Information about a discovered TypeScript project
/// </summary>
public class TypeScriptProjectInfo
{
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public bool HasTsConfig { get; set; }
    public bool HasPackageJson { get; set; }
    public int SourceFileCount { get; set; }
    public int TestFileCount { get; set; }
    public int DeclarationFileCount { get; set; }
    public bool IsAngularProject { get; set; }
    public bool IsReactProject { get; set; }
    public DateTime DiscoveredAt { get; set; }
}
