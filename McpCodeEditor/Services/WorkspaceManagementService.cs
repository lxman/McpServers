using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using McpCodeEditor.Interfaces;
using System.Reflection;

namespace McpCodeEditor.Services;

/// <summary>
/// Service responsible for managing Roslyn workspaces (MSBuild and fallback modes)
/// Extracted from SymbolNavigationService - Phase 4 Task 2a
/// </summary>
public class WorkspaceManagementService : IWorkspaceManagementService
{
    private readonly CodeEditorConfigurationService _config;
    private readonly DeveloperEnvironmentService _devEnvironment;
    private readonly ILogger<WorkspaceManagementService>? _logger;
    private readonly object _workspaceLock = new();
    
    private Workspace? _workspace;
    private Solution? _currentSolution;
    private Dictionary<string, Project> _projectCache = new();
    private DateTime _lastWorkspaceRefresh = DateTime.MinValue;
    private bool _useFallbackWorkspace = false;
    private bool _environmentInitialized = false;

    public WorkspaceManagementService(
        CodeEditorConfigurationService config,
        DeveloperEnvironmentService devEnvironment,
        ILogger<WorkspaceManagementService>? logger = null)
    {
        _config = config;
        _devEnvironment = devEnvironment;
        _logger = logger;
    }

    public Solution? CurrentSolution => _currentSolution;
    public bool IsUsingFallbackWorkspace => _useFallbackWorkspace;
    public bool IsEnvironmentInitialized => _environmentInitialized;
    public IReadOnlyDictionary<string, Project> ProjectCache => _projectCache.AsReadOnly();

    /// <summary>
    /// Initialize or refresh the workspace for symbol navigation
    /// </summary>
    public async Task<bool> RefreshWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Initialize developer environment if not already done
            if (!_environmentInitialized)
            {
                _logger?.LogInformation("Initializing developer environment...");
                _environmentInitialized = _devEnvironment.Initialize();
                
                if (_environmentInitialized)
                {
                    _logger?.LogInformation("Developer environment initialized successfully");
                    Dictionary<string, string> envInfo = _devEnvironment.GetEnvironmentInfo();
                    foreach (KeyValuePair<string, string> kvp in envInfo)
                    {
                        _logger?.LogInformation($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                else
                {
                    _logger?.LogWarning("Failed to initialize developer environment - will use fallback");
                }
            }

            lock (_workspaceLock)
            {
                _workspace?.Dispose();
                _workspace = null;
                _projectCache.Clear();
            }

            // Try to create MSBuildWorkspace first (if environment was initialized)
            if (!_useFallbackWorkspace && _environmentInitialized)
            {
                try
                {
                    _logger?.LogInformation("Attempting to create MSBuildWorkspace...");
                    
                    _workspace = MSBuildWorkspace.Create();
                    _logger?.LogInformation("MSBuildWorkspace created successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to create MSBuildWorkspace, falling back to AdhocWorkspace");
                    _useFallbackWorkspace = true;
                }
            }

            // Use fallback AdhocWorkspace if MSBuild isn't available
            if (_workspace == null || _useFallbackWorkspace || !_environmentInitialized)
            {
                _logger?.LogInformation("Creating AdhocWorkspace as fallback...");
                _workspace = new AdhocWorkspace();
                return await LoadProjectWithFallbackAsync(cancellationToken);
            }

            // Load with MSBuildWorkspace
            return await LoadProjectWithMSBuildAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh workspace");
            Console.WriteLine($"Failed to refresh workspace: {ex.Message}");
            
            // Last resort - create minimal workspace
            try
            {
                _logger?.LogInformation("Attempting last resort workspace creation...");
                var adhocWorkspace = new AdhocWorkspace();
                _workspace = adhocWorkspace;
                _useFallbackWorkspace = true;
                
                // Create a minimal project
                var projectId = ProjectId.CreateNewId();
                var projectInfo = Microsoft.CodeAnalysis.ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    "MinimalProject",
                    "MinimalProject",
                    LanguageNames.CSharp);
                
                // Add project to the solution
                Solution solution = adhocWorkspace.CurrentSolution;
                solution = solution.AddProject(projectInfo);
                
                // Apply changes to workspace
                if (!adhocWorkspace.TryApplyChanges(solution))
                {
                    _logger?.LogWarning("Failed to apply changes to workspace");
                }
                
                _currentSolution = adhocWorkspace.CurrentSolution;
                Project? project = _currentSolution.GetProject(projectId);
                if (project != null)
                {
                    _projectCache["MinimalProject"] = project;
                }
                _lastWorkspaceRefresh = DateTime.Now;
                
                _logger?.LogInformation("Created minimal workspace successfully");
                return true;
            }
            catch (Exception minimalEx)
            {
                _logger?.LogError(minimalEx, "Failed to create even minimal workspace");
                return false;
            }
        }
    }

    private async Task<bool> LoadProjectWithMSBuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            string workspaceRoot = _config.DefaultWorkspace;
            
            // Validate workspace root exists
            if (!Directory.Exists(workspaceRoot))
            {
                _logger?.LogError($"Workspace directory does not exist: {workspaceRoot}");
                return false;
            }
            
            var msbuildWorkspace = (MSBuildWorkspace)_workspace!;

            // Find solution or project files
            string[] solutionFiles = Directory.GetFiles(workspaceRoot, "*.sln", SearchOption.AllDirectories);
            string[] projectFiles = Directory.GetFiles(workspaceRoot, "*.csproj", SearchOption.AllDirectories);

            _logger?.LogInformation($"Found {solutionFiles.Length} solution files and {projectFiles.Length} project files");

            if (solutionFiles.Any())
            {
                string solutionPath = solutionFiles.First();
                _logger?.LogInformation($"Loading solution: {solutionPath}");
                _currentSolution = await msbuildWorkspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);

                foreach (Project project in _currentSolution.Projects)
                {
                    _projectCache[project.FilePath ?? project.Name] = project;
                }
            }
            else if (projectFiles.Any())
            {
                // Load the first project and use its solution
                string firstProjectPath = projectFiles.First();
                _logger?.LogInformation($"Loading project: {firstProjectPath}");
                Project firstProject = await msbuildWorkspace.OpenProjectAsync(firstProjectPath, cancellationToken: cancellationToken);
                _currentSolution = firstProject.Solution;
                _projectCache[firstProjectPath] = firstProject;

                // Add other projects to the solution
                foreach (string projectPath in projectFiles.Skip(1).Take(9)) // Limit to avoid performance issues
                {
                    try
                    {
                        Project project = await msbuildWorkspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
                        _projectCache[projectPath] = project;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Failed to load project {projectPath}");
                        Console.WriteLine($"Failed to load project {projectPath}: {ex.Message}");
                    }
                }
            }
            else
            {
                _logger?.LogWarning("No solution or project files found in workspace");
                return false;
            }

            _lastWorkspaceRefresh = DateTime.Now;
            return _currentSolution != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load project with MSBuild");
            return false;
        }
    }

    private async Task<bool> LoadProjectWithFallbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            string workspaceRoot = _config.DefaultWorkspace;
            
            // Validate workspace root exists
            if (!Directory.Exists(workspaceRoot))
            {
                _logger?.LogError($"Workspace directory does not exist: {workspaceRoot}");
                return false;
            }
            
            var adhocWorkspace = (AdhocWorkspace)_workspace!;

            // Create a simple project with C# files in the workspace
            var projectId = ProjectId.CreateNewId();
            var projectInfo = Microsoft.CodeAnalysis.ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "FallbackProject",
                "FallbackProject",
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: new CSharpParseOptions(LanguageVersion.Latest));

            // Add project to the solution
            Solution solution = adhocWorkspace.CurrentSolution;
            solution = solution.AddProject(projectInfo);
            
            // Add default references
            PortableExecutableReference mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            PortableExecutableReference systemCore = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
            PortableExecutableReference systemRuntime = MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location);
            
            solution = solution
                .AddMetadataReference(projectId, mscorlib)
                .AddMetadataReference(projectId, systemCore)
                .AddMetadataReference(projectId, systemRuntime);

            // Add C# files to the project
            List<string> csFiles = Directory.GetFiles(workspaceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\"))
                .Take(100).ToList(); // Limit for performance

            _logger?.LogInformation($"Found {csFiles.Count} C# files to add to fallback workspace");

            var filesAdded = 0;
            foreach (string filePath in csFiles)
            {
                try
                {
                    string fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
                    var documentInfo = DocumentInfo.Create(
                        DocumentId.CreateNewId(projectId),
                        Path.GetFileName(filePath),
                        loader: TextLoader.From(TextAndVersion.Create(
                            SourceText.From(fileContent), 
                            VersionStamp.Create())),
                        filePath: filePath);

                    solution = solution.AddDocument(documentInfo);
                    filesAdded++;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Failed to add document {filePath}");
                }
            }

            // Apply all changes to the workspace
            if (!adhocWorkspace.TryApplyChanges(solution))
            {
                _logger?.LogWarning("Some changes could not be applied to the workspace");
            }

            // Update our references
            _currentSolution = adhocWorkspace.CurrentSolution;
            Project? project = _currentSolution.GetProject(projectId);
            if (project != null)
            {
                _projectCache[project.Name] = project;
            }
            _lastWorkspaceRefresh = DateTime.Now;

            _logger?.LogInformation($"Loaded {filesAdded} documents in fallback mode");
            return filesAdded > 0 || project != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load project with fallback");
            return false;
        }
    }

    /// <summary>
    /// Get environment initialization status
    /// </summary>
    public Dictionary<string, string> GetEnvironmentStatus()
    {
        var status = new Dictionary<string, string>();
        status["EnvironmentInitialized"] = _environmentInitialized.ToString();
        status["WorkspaceMode"] = _useFallbackWorkspace ? "Fallback" : "MSBuild";
        status["WorkspaceLoaded"] = (_currentSolution != null).ToString();
        status["ProjectCount"] = _projectCache.Count.ToString();
        
        if (_environmentInitialized)
        {
            Dictionary<string, string> envInfo = _devEnvironment.GetEnvironmentInfo();
            foreach (KeyValuePair<string, string> kvp in envInfo)
            {
                status[$"Env_{kvp.Key}"] = kvp.Value;
            }
        }
        
        return status;
    }

    public void Dispose()
    {
        _workspace?.Dispose();
    }
}
