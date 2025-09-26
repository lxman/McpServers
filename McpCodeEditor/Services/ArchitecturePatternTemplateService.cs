using McpCodeEditor.Models;

namespace McpCodeEditor.Services;

/// <summary>
/// Service responsible for managing architecture pattern templates
/// </summary>
public class ArchitecturePatternTemplateService
{
    private readonly List<ArchitecturePatternTemplate> _patternTemplates = InitializePatternTemplates();

    /// <summary>
    /// Get all available pattern templates
    /// </summary>
    public IReadOnlyList<ArchitecturePatternTemplate> GetAllTemplates()
    {
        return _patternTemplates.AsReadOnly();
    }

    /// <summary>
    /// Get a specific pattern template by type
    /// </summary>
    public ArchitecturePatternTemplate? GetTemplate(ArchitectureType type)
    {
        return _patternTemplates.FirstOrDefault(t => t.Type == type);
    }

    /// <summary>
    /// Get minimum confidence threshold for a pattern type
    /// </summary>
    public double GetMinConfidenceThreshold(ArchitectureType type)
    {
        ArchitecturePatternTemplate? template = GetTemplate(type);
        return template?.MinConfidenceThreshold ?? 0.3; // LOWERED default from 0.6
    }

    /// <summary>
    /// Initialize pattern templates for detection
    /// </summary>
    private static List<ArchitecturePatternTemplate> InitializePatternTemplates()
    {
        return
        [
            new ArchitecturePatternTemplate
            {
                Type = ArchitectureType.AngularDotNetApi,
                Name = "Angular + .NET API",
                Description = "Angular frontend with .NET backend API",
                FileIndicators = ["angular.json", "*.csproj", "Controllers/*.cs"],
                DirectoryIndicators = ["src/app", "Controllers", "Models"],
                DependencyIndicators = ["@angular/core", "Microsoft.AspNetCore"],
                RequiredTechnologies = ["Angular", ".NET"],
                MinConfidenceThreshold = 0.4 // LOWERED from 0.7
            },

            new ArchitecturePatternTemplate
            {
                Type = ArchitectureType.ReactNodeJsDatabase,
                Name = "React + Node.js",
                Description = "React frontend with Node.js backend",
                FileIndicators = ["package.json", "src/App.js", "server.js"],
                DependencyIndicators = ["react", "express", "node"],
                RequiredTechnologies = ["React", "Node.js"],
                MinConfidenceThreshold = 0.4 // LOWERED from 0.6
            },

            new ArchitecturePatternTemplate
            {
                Type = ArchitectureType.McpServerClient,
                Name = "MCP Server/Client",
                Description = "Model Context Protocol architecture",
                FileIndicators = ["mcp.json", "server.js", "client.js"],
                NamingPatterns = ["*mcp*server*", "*mcp*client*"],
                RequiredTechnologies = ["MCP"],
                MinConfidenceThreshold = 0.5 // LOWERED from 0.8
            },

            new ArchitecturePatternTemplate
            {
                Type = ArchitectureType.WpfDotNetSharedLibs,
                Name = "WPF + .NET Libraries",
                Description = "WPF application with shared .NET libraries",
                FileIndicators = ["*.csproj", "App.xaml", "MainWindow.xaml"],
                DirectoryIndicators = ["Properties", "Resources"],
                DependencyIndicators = ["Microsoft.WindowsDesktop.App"],
                RequiredTechnologies = ["WPF", ".NET"],
                MinConfidenceThreshold = 0.4
            },

            new ArchitecturePatternTemplate
            {
                Type = ArchitectureType.MonoRepoMultiProject,
                Name = "MonoRepo Multi-Project",
                Description = "Multiple projects in single repository",
                FileIndicators = ["lerna.json", "nx.json", "rush.json", "pnpm-workspace.yaml"],
                DirectoryIndicators = ["packages", "apps", "libs"],
                RequiredTechnologies = ["MonoRepo"],
                MinConfidenceThreshold = 0.5
            },

            new ArchitecturePatternTemplate
            {
                Type = ArchitectureType.FrontendBackendSeparated,
                Name = "Separated Frontend/Backend",
                Description = "Frontend and backend projects in separate directories",
                DirectoryIndicators = ["frontend", "backend", "client", "server"],
                RequiredTechnologies = ["Frontend", "Backend"],
                MinConfidenceThreshold = 0.4
            }
        ];
    }
}
