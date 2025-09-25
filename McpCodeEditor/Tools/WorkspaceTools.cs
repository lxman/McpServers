using System.ComponentModel;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Tools.Common;

namespace McpCodeEditor.Tools;

public class WorkspaceTools(
    CodeEditorConfigurationService config,
    ProjectDetectionService projectDetection,
    ProjectScaleService projectScale,
    GitService gitService,
    IRefactoringOrchestrator refactoringOrchestrator,
    WorkspaceAnalysisService workspaceAnalysis,
    WorkspaceStatsService workspaceStats,
    WorkspaceInfoService info)
    : BaseToolClass
{
    private readonly ProjectScaleService _projectScale = projectScale;
    private readonly GitService _gitService = gitService;

    #region Workspace Management

    [McpServerTool]
    [Description("Detect projects and suggest workspaces based on common development patterns")]
    public async Task<string> WorkspaceDetectAsync(
        [Description("Maximum number of projects to return")]
        int maxResults = 20)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            if (maxResults <= 0)
            {
                throw new ArgumentException("Max results must be positive.", nameof(maxResults));
            }

            var projects = await projectDetection.DetectWorkspacesAsync();
            var result = new
            {
                success = true,
                current_workspace = config.DefaultWorkspace,
                smart_detected = config.SmartDetectedWorkspace,
                auto_detect_enabled = config.Workspace.AutoDetectWorkspace,
                projects = projects.Take(maxResults).Select(p => new
                {
                    path = p.Path,
                    name = p.Name,
                    type = p.Type.ToString(),
                    description = p.Description,
                    indicators = p.Indicators,
                    score = p.Score
                }).ToArray()
            };

            return result;
        });
    }

    [McpServerTool]
    [Description("Switch to a different workspace directory")]
    public async Task<string> WorkspaceSwitchAsync(
        [Description("Path to the new workspace directory")]
        string workspacePath)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            ValidateRequiredParameter(workspacePath, nameof(workspacePath));

            if (!Directory.Exists(workspacePath))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {workspacePath}");
            }

            await config.SetPreferredWorkspaceAsync(workspacePath);

            var result = new
            {
                success = true,
                message = $"Switched to workspace: {workspacePath}",
                new_workspace = config.DefaultWorkspace,
                // RS-001: Using extracted service instead of helper method
                workspace_info = await workspaceAnalysis.AnalyzeWorkspaceAsync(workspacePath)
            };

            return result;
        });
    }

    [McpServerTool]
    [Description("Get intelligent workspace suggestions based on the current context")]
    public async Task<string> WorkspaceSuggestAsync(
        [Description("Include workspace history")]
        bool includeHistory = true)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var suggested = await projectDetection.SuggestBestWorkspaceAsync();
            var allProjects = await projectDetection.DetectWorkspacesAsync();

            var result = new
            {
                success = true,
                current_workspace = config.DefaultWorkspace,
                suggested_workspace = suggested != null ? new
                {
                    path = suggested.Path,
                    name = suggested.Name,
                    type = suggested.Type.ToString(),
                    description = suggested.Description,
                    score = suggested.Score,
                    reason = "Highest-scored project with recent activity"
                } : null,
                recent_workspaces = includeHistory ? config.Workspace.WorkspaceHistory.Take(5).ToArray() : [],
                top_projects = allProjects.Take(5).Select(p => new
                {
                    path = p.Path,
                    name = p.Name,
                    type = p.Type.ToString(),
                    score = p.Score,
                    description = p.Description
                }).ToArray()
            };

            return result;
        });
    }

    [McpServerTool]
    [Description("Refresh workspace detection and re-analyze current context")]
    public async Task<string> WorkspaceRefreshAsync()
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            await config.RefreshWorkspaceDetectionAsync();

            var result = new
            {
                success = true,
                message = "Workspace detection refreshed",
                current_workspace = config.DefaultWorkspace,
                smart_detected = config.SmartDetectedWorkspace
            };

            return result;
        });
    }

    [McpServerTool]
    [Description("Get information about the current workspace")]
    public async Task<string> WorkspaceInfoAsync(
        [Description("Include file statistics")]
        bool includeStats = true,
        [Description("Include git information")]
        bool includeGit = true)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var workspaceInfo = new
            {
                success = true,
                workspace_path = config.DefaultWorkspace,
                smart_detected_workspace = config.SmartDetectedWorkspace,
                workspace_settings = new
                {
                    auto_detect_enabled = config.Workspace.AutoDetectWorkspace,
                    preferred_workspace = config.Workspace.PreferredWorkspace,
                    workspace_history = config.Workspace.WorkspaceHistory
                },
                server_version = "3.0.0",  // Updated version with TECH-007 SOLID refactoring architecture
                configuration = new
                {
                    allowed_extensions = config.AllowedExtensions,
                    excluded_directories = config.ExcludedDirectories,
                    max_file_size = config.MaxFileSize,
                    security_enabled = config.Security.RestrictToWorkspace
                },
                capabilities = new
                {
                    file_operations = true,
                    code_analysis = config.CodeAnalysis.EnableSyntaxHighlighting,
                    diff_generation = true,
                    search = true,
                    syntax_highlighting = config.CodeAnalysis.EnableSyntaxHighlighting,
                    workspace_detection = true,
                    project_analysis = true,
                    git_operations = true,
                    // TECH-007: SOLID-compliant refactoring architecture
                    refactoring = new
                    {
                        architecture = "SOLID-compliant RefactoringOrchestrator with language-specific services",
                        symbol_rename = true,
                        method_extraction = true,
                        method_inlining = true,
                        variable_introduction = true,
                        field_encapsulation = true,
                        import_organization = true,
                        using_management = true,
                        supported_languages = refactoringOrchestrator.GetSupportedLanguages().Select(l => l.ToString()).ToArray(),
                        // Enhanced: Clean Architecture with focused services
                        architecture_principles = new
                        {
                            single_responsibility = "Each service has one focused responsibility",
                            open_closed = "Easy to add new languages and operations",
                            liskov_substitution = "Language services implement common interfaces",
                            interface_segregation = "Clients depend only on interfaces they use",
                            dependency_inversion = "All dependencies on abstractions"
                        },
                        service_structure = new
                        {
                            orchestrator = "RefactoringOrchestrator - Main coordination service",
                            csharp_services = new[] { "CSharpMethodExtractor", "CSharpImportManager", "CSharpVariableOperations", "CSharpMethodInliner" },
                            typescript_services = new[] { "TypeScriptMethodExtractor", "TypeScriptImportManager", "TypeScriptVariableOperations", "TypeScriptMethodInliner" },
                            utility_services = new[] { "SyntaxHelpers", "IdentifierValidation", "CodeFormatting" },
                            security_services = new[] { "PathValidationService" }
                        },
                        // TypeScript/JavaScript support
                        typescript_support = new
                        {
                            imports = new
                            {
                                organize_imports = true,
                                add_imports = true,
                                group_by_type = true,
                                sort_alphabetically = true,
                                supported_extensions = new[] { ".ts", ".tsx", ".js", ".jsx" }
                            },
                            refactoring = new
                            {
                                extract_method = true,
                                introduce_variable = true,
                                inline_function = true,
                                function_types = new[] { "function", "arrow", "async", "async-arrow" },
                                export_options = new[] { "none", "export", "export-default" },
                                variable_declarations = new[] { "const", "let", "var" },
                                scope_options = new[] { "file", "project" },
                                implementation_status = "TECH-007_SLICE_10_COMPLETE_ORCHESTRATED_ARCHITECTURE"
                            }
                        }
                    },
                    backup_management = true,
                    change_tracking = true,
                    // UX-008: Intelligent Project Scale Detection
                    project_scale_analysis = new
                    {
                        intelligent_filtering = true,
                        build_artifact_detection = true,
                        source_only_metrics = true,
                        scale_classification = true,
                        complexity_scoring = true,
                        supported_patterns = new[]
                        {
                            "Node.js (node_modules, dist, build)",
                            ".NET (bin, obj, packages)",
                            "Java (target, .m2)",
                            "Python (__pycache__, venv)",
                            "Git repositories (.git)",
                            "IDE files (.vs, .idea, .vscode)",
                            "Generated files (*.generated.cs, *.min.js)"
                        }
                    },
                    // TECH-004: Multi-Platform Project Intelligence
                    architecture_detection = new
                    {
                        multi_platform_recognition = true,
                        pattern_detection = true,
                        relationship_analysis = true,
                        workspace_suggestions = true,
                        supported_patterns = new[]
                        {
                            "Angular + .NET API",
                            "React + Node.js",
                            "MCP Server/Client",
                            "MonoRepo Multi-Project",
                            "WPF + .NET + Shared Libraries",
                            "Frontend/Backend Separated"
                        }
                    },
                    // Advanced Code Generation capabilities
                    code_generation = new
                    {
                        generate_constructor = true,
                        generate_equals = true,
                        generate_gethashcode = true,
                        generate_tostring = true,
                        generate_properties = true,
                        implement_interface = true,
                        roslyn_powered = true
                    }
                },
                // RS-001: Using extracted services instead of helper methods
                stats = includeStats ? await workspaceStats.GetIntelligentWorkspaceStatsAsync() : null,
                git_info = includeGit ? await info.GetAdvancedGitInfoAsync() : null
            };

            return workspaceInfo;
        });
    }

    #endregion
}
