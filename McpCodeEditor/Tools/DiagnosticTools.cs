using System.ComponentModel;
using System.Text;
using McpCodeEditor.Interfaces;
using McpCodeEditor.Models;
using ModelContextProtocol.Server;
using McpCodeEditor.Services;
using McpCodeEditor.Services.TypeScript;
using McpCodeEditor.Tools.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace McpCodeEditor.Tools;

/// <summary>
/// MCP tools for diagnostics and service verification
/// </summary>
[McpServerToolType]
public class DiagnosticTools : BaseToolClass
{
    private readonly ILogger<DiagnosticTools>? _logger;
    private readonly SymbolNavigationService? _symbolNavigationService;
    private readonly TypeScriptAstParserService? _typeScriptAstParser;
    private readonly CodeEditorConfigurationService? _configService;
    private readonly DeveloperEnvironmentService? _devEnvironmentService;
    private readonly IServiceProvider _serviceProvider;

    public DiagnosticTools(
        IServiceProvider serviceProvider,
        ILogger<DiagnosticTools>? logger = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Try to resolve services, but don't fail if they're not available
        _symbolNavigationService = serviceProvider.GetService<SymbolNavigationService>();
        _typeScriptAstParser = serviceProvider.GetService<TypeScriptAstParserService>();
        _configService = serviceProvider.GetService<CodeEditorConfigurationService>();
        _devEnvironmentService = serviceProvider.GetService<DeveloperEnvironmentService>();
    }

    [McpServerTool]
    [Description("Get the status of all core services and verify their initialization")]
    public async Task<string> GetServiceStatusAsync()
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var status = new StringBuilder();
            status.AppendLine("=== MCP Code Editor Service Status ===");
            status.AppendLine();
            
            // Check Developer Environment Service
            status.AppendLine($"Developer Environment Service: {(_devEnvironmentService != null ? "✓ Initialized" : "✗ Not initialized")}");
            if (_devEnvironmentService != null)
            {
                status.AppendLine($"  Environment Initialized: {(_devEnvironmentService.IsInitialized ? "✓ Yes" : "✗ No")}");
                
                Dictionary<string, string> envInfo = _devEnvironmentService.GetEnvironmentInfo();
                foreach (KeyValuePair<string, string> kvp in envInfo)
                {
                    status.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            
            status.AppendLine();
            
            // Check configuration service
            status.AppendLine($"Configuration Service: {(_configService != null ? "✓ Initialized" : "✗ Not initialized")}");
            if (_configService != null)
            {
                status.AppendLine($"  Default Workspace: {_configService.DefaultWorkspace}");
                status.AppendLine($"  Workspace History Count: {_configService.Workspace.WorkspaceHistory.Count}");
            }
            
            // Check Symbol Navigation Service
            status.AppendLine($"Symbol Navigation Service: {(_symbolNavigationService != null ? "✓ Initialized" : "✗ Not initialized")}");
            if (_symbolNavigationService != null)
            {
                try
                {
                    // Try to refresh the workspace to see if it works
                    bool refreshed = await _symbolNavigationService.RefreshWorkspaceAsync();
                    status.AppendLine($"  Workspace Refresh: {(refreshed ? "✓ Success" : "✗ Failed")}");
                    
                    // Get environment status from the service
                    Dictionary<string, string> envStatus = _symbolNavigationService.GetEnvironmentStatus();
                    foreach (KeyValuePair<string, string> kvp in envStatus)
                    {
                        status.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                catch (Exception ex)
                {
                    status.AppendLine($"  Workspace Refresh: ✗ Error - {ex.Message}");
                }
            }
            
            // Check TypeScript AST Parser
            status.AppendLine($"TypeScript AST Parser Service: {(_typeScriptAstParser != null ? "✓ Initialized" : "✗ Not initialized")}");
            if (_typeScriptAstParser != null)
            {
                try
                {
                    // Try a simple parse to see if Node.js integration works
                    var testCode = "const x = 1;";
                    TypeScriptAst? ast = await _typeScriptAstParser.ParseAsync(testCode, "test.ts");
                    status.AppendLine($"  Node.js Integration: {(ast != null ? "✓ Working" : "✗ Not working")}");
                }
                catch (Exception ex)
                {
                    status.AppendLine($"  Node.js Integration: ✗ Error - {ex.Message}");
                }
            }
            
            // Check Node.js availability
            status.AppendLine();
            status.AppendLine("=== Environment Checks ===");
            try
            {
                string nodeModulesPath = Path.Combine(AppContext.BaseDirectory, "node_modules");
                status.AppendLine($"Node Modules Directory: {(Directory.Exists(nodeModulesPath) ? "✓ Exists" : "✗ Not found")}");
                
                if (Directory.Exists(nodeModulesPath))
                {
                    string typescriptPath = Path.Combine(nodeModulesPath, "typescript");
                    status.AppendLine($"  TypeScript Package: {(Directory.Exists(typescriptPath) ? "✓ Installed" : "✗ Not installed")}");
                }
                
                string scriptsPath = Path.Combine(AppContext.BaseDirectory, "Scripts");
                status.AppendLine($"Scripts Directory: {(Directory.Exists(scriptsPath) ? "✓ Exists" : "✗ Not found")}");
                
                if (Directory.Exists(scriptsPath))
                {
                    string parserScript = Path.Combine(scriptsPath, "typescript-parser.js");
                    status.AppendLine($"  TypeScript Parser Script: {(File.Exists(parserScript) ? "✓ Found" : "✗ Not found")}");
                }
            }
            catch (Exception ex)
            {
                status.AppendLine($"Environment check error: {ex.Message}");
            }
            
            // Check all registered services
            status.AppendLine();
            status.AppendLine("=== Service Provider Status ===");
            
            // List key services and their registration status
            (string, Type)[] servicesToCheck =
            [
                ("DeveloperEnvironmentService", typeof(DeveloperEnvironmentService)),
                ("SymbolNavigationService", typeof(SymbolNavigationService)),
                ("TypeScriptAstParserService", typeof(TypeScriptAstParserService)),
                ("CodeEditorConfigurationService", typeof(CodeEditorConfigurationService)),
                ("SearchService", typeof(SearchService)),
                ("FileOperationsService", typeof(FileOperationsService)),
                ("GitService", typeof(GitService)),
                ("BackupService", typeof(IBackupService)),
                ("ChangeTrackingService", typeof(ChangeTrackingService))
            ];
            
            foreach ((string name, Type type) in servicesToCheck)
            {
                object? service = _serviceProvider.GetService(type);
                status.AppendLine($"{name}: {(service != null ? "✓ Registered" : "✗ Not registered")}");
            }
            
            return status.ToString();
        });
    }

    [McpServerTool]
    [Description("Initialize the developer environment manually")]
    public async Task<string> InitializeDeveloperEnvironmentAsync()
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            if (_devEnvironmentService == null)
            {
                return "Developer Environment Service is not available. Cannot initialize.";
            }

            var result = new StringBuilder();
            result.AppendLine("=== Developer Environment Initialization ===");
            
            if (_devEnvironmentService.IsInitialized)
            {
                result.AppendLine("Environment is already initialized.");
            }
            else
            {
                result.AppendLine("Attempting to initialize developer environment...");
                bool success = _devEnvironmentService.Initialize();
                
                if (success)
                {
                    result.AppendLine("✓ Environment initialized successfully!");
                }
                else
                {
                    result.AppendLine("✗ Failed to initialize environment.");
                }
            }
            
            result.AppendLine();
            result.AppendLine("Current Environment Status:");
            Dictionary<string, string> envInfo = _devEnvironmentService.GetEnvironmentInfo();
            foreach (KeyValuePair<string, string> kvp in envInfo)
            {
                result.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            
            // Now try to refresh the symbol navigation workspace
            if (_symbolNavigationService != null)
            {
                result.AppendLine();
                result.AppendLine("Attempting to refresh Symbol Navigation workspace...");
                try
                {
                    bool refreshed = await _symbolNavigationService.RefreshWorkspaceAsync();
                    result.AppendLine(refreshed ? "✓ Workspace refreshed successfully!" : "✗ Failed to refresh workspace.");
                    
                    Dictionary<string, string> navStatus = _symbolNavigationService.GetEnvironmentStatus();
                    result.AppendLine();
                    result.AppendLine("Symbol Navigation Status:");
                    foreach (KeyValuePair<string, string> kvp in navStatus)
                    {
                        result.AppendLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                catch (Exception ex)
                {
                    result.AppendLine($"✗ Error refreshing workspace: {ex.Message}");
                }
            }
            
            return result.ToString();
        });
    }

    [McpServerTool]
    [Description("Test symbol navigation service with a sample C# file")]
    public async Task<string> TestSymbolNavigationAsync(
        [Description("Path to a C# file to test with")] string? filePath = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            if (_symbolNavigationService == null)
            {
                return "Symbol Navigation Service is not initialized. Cannot perform test.";
            }

            var result = new StringBuilder();
            result.AppendLine("=== Symbol Navigation Test ===");
            
            // If no file path provided, create a test file
            if (string.IsNullOrEmpty(filePath))
            {
                string tempDir = Path.GetTempPath();
                filePath = Path.Combine(tempDir, "test_symbol_nav.cs");
                
                // Create a simple test C# file
                var testContent = @"using System;

namespace TestNamespace
{
    public class TestClass
    {
        private int _field;
        
        public int Property { get; set; }
        
        public void TestMethod()
        {
            var localVar = 42;
            Console.WriteLine(localVar);
        }
        
        private static string HelperMethod(string input)
        {
            return input.ToUpper();
        }
    }
}";
                await File.WriteAllTextAsync(filePath, testContent);
                result.AppendLine($"Created test file: {filePath}");
            }
            
            // Test workspace refresh
            result.AppendLine("\n1. Testing workspace refresh...");
            try
            {
                bool refreshed = await _symbolNavigationService.RefreshWorkspaceAsync();
                result.AppendLine($"   Result: {(refreshed ? "✓ Success" : "✗ Failed")}");
            }
            catch (Exception ex)
            {
                result.AppendLine($"   Result: ✗ Error - {ex.Message}");
            }
            
            // Test Go to Definition
            result.AppendLine("\n2. Testing Go to Definition (line 14, column 31 - 'localVar')...");
            try
            {
                SymbolNavigationResult goToDefResult = await _symbolNavigationService.GoToDefinitionAsync(filePath, 14, 31);
                result.AppendLine($"   Success: {goToDefResult.Success}");
                if (goToDefResult.Success)
                {
                    result.AppendLine($"   Message: {goToDefResult.Message}");
                    result.AppendLine($"   Locations found: {goToDefResult.Locations.Count}");
                }
                else
                {
                    result.AppendLine($"   Error: {goToDefResult.Error}");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"   Result: ✗ Error - {ex.Message}");
            }
            
            // Test Find References
            result.AppendLine("\n3. Testing Find References (line 9, column 20 - 'Property')...");
            try
            {
                SymbolNavigationResult findRefsResult = await _symbolNavigationService.FindReferencesAsync(filePath, 9, 20);
                result.AppendLine($"   Success: {findRefsResult.Success}");
                if (findRefsResult.Success)
                {
                    result.AppendLine($"   Message: {findRefsResult.Message}");
                    result.AppendLine($"   References found: {findRefsResult.Locations.Count}");
                }
                else
                {
                    result.AppendLine($"   Error: {findRefsResult.Error}");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"   Result: ✗ Error - {ex.Message}");
            }
            
            // Test Find Symbols by Name
            result.AppendLine("\n4. Testing Find Symbols by Name ('TestMethod')...");
            try
            {
                SymbolNavigationResult findSymbolsResult = await _symbolNavigationService.FindSymbolsByNameAsync("TestMethod");
                result.AppendLine($"   Success: {findSymbolsResult.Success}");
                if (findSymbolsResult.Success)
                {
                    result.AppendLine($"   Message: {findSymbolsResult.Message}");
                    result.AppendLine($"   Symbols found: {findSymbolsResult.Locations.Count}");
                }
                else
                {
                    result.AppendLine($"   Error: {findSymbolsResult.Error}");
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"   Result: ✗ Error - {ex.Message}");
            }
            
            return result.ToString();
        });
    }

    [McpServerTool]
    [Description("Test TypeScript AST parser service")]
    public async Task<string> TestTypeScriptParserAsync(
        [Description("Optional TypeScript code to parse")] string? code = null)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            if (_typeScriptAstParser == null)
            {
                return "TypeScript AST Parser Service is not initialized. Cannot perform test.";
            }

            var result = new StringBuilder();
            result.AppendLine("=== TypeScript AST Parser Test ===");
            
            // Use provided code or default test code
            if (string.IsNullOrEmpty(code))
            {
                code = @"import { Component } from '@angular/core';

interface User {
    id: number;
    name: string;
}

export class TestClass {
    private users: User[] = [];
    
    constructor(private readonly service: DataService) {}
    
    async loadData(): Promise<void> {
        const data = await this.service.fetchUsers();
        this.users = data;
    }
    
    getUser(id: number): User | undefined {
        return this.users.find(u => u.id === id);
    }
}";
            }
            
            result.AppendLine($"Parsing {code.Length} characters of TypeScript code...\n");
            
            try
            {
                TypeScriptAst ast = await _typeScriptAstParser.ParseAsync(code, "test.ts");
                
                result.AppendLine($"✓ Parse successful!");
                result.AppendLine($"  Nodes: {ast.Nodes?.Count ?? 0}");
                result.AppendLine($"  Imports: {ast.Imports?.Count ?? 0}");
                if (ast.Imports?.Count > 0)
                {
                    foreach (ImportInfo import in ast.Imports)
                    {
                        result.AppendLine($"    - from '{import.Module}'");
                    }
                }
                
                result.AppendLine($"  Classes: {ast.Classes?.Count ?? 0}");
                if (ast.Classes?.Count > 0)
                {
                    foreach (ClassInfo cls in ast.Classes)
                    {
                        result.AppendLine($"    - {cls.Name} ({cls.Members?.Count ?? 0} members)");
                    }
                }
                
                result.AppendLine($"  Functions: {ast.Functions?.Count ?? 0}");
                if (ast.Functions?.Count > 0)
                {
                    foreach (FunctionInfo func in ast.Functions)
                    {
                        result.AppendLine($"    - {func.Name}{(func.IsAsync ? " (async)" : "")}");
                    }
                }
                
                result.AppendLine($"  Variables: {ast.Variables?.Count ?? 0}");
                result.AppendLine($"  Diagnostics: {ast.Diagnostics?.Count ?? 0}");
                
                if (ast.Diagnostics?.Count > 0)
                {
                    result.AppendLine("\nDiagnostics:");
                    foreach (DiagnosticInfo diag in ast.Diagnostics.Take(5))
                    {
                        result.AppendLine($"  - Line {diag.Start.Line + 1}: {diag.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.AppendLine($"✗ Parse failed: {ex.Message}");
                
                // Try to provide more details about the error
                if (ex.InnerException != null)
                {
                    result.AppendLine($"  Inner error: {ex.InnerException.Message}");
                }
                
                result.AppendLine("\nPossible causes:");
                result.AppendLine("  1. Node.js is not installed or not in PATH");
                result.AppendLine("  2. TypeScript npm package is not installed");
                result.AppendLine("  3. The typescript-parser.js script has errors");
                result.AppendLine("  4. Node.js process options are misconfigured");
            }
            
            return result.ToString();
        });
    }

    [McpServerTool]
    [Description("Get detailed logging information from the application")]
    public static async Task<string> GetApplicationLogsAsync(
        [Description("Number of recent log entries to retrieve")] int count = 50)
    {
        return await ExecuteWithErrorHandlingAsync(async () =>
        {
            var result = new StringBuilder();
            result.AppendLine("=== Application Logs ===");
            
            // Look for log files with date stamp pattern
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Claude", "logs");
            
            if (Directory.Exists(logDir))
            {
                result.AppendLine($"Log directory: {logDir}");
                
                // Find log files matching the pattern: mcp-code-editor-debug*.log
                string[] logFiles = Directory.GetFiles(logDir, "mcp-code-editor-debug*.log")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray();
                
                if (logFiles.Length > 0)
                {
                    string mostRecentLog = logFiles[0];
                    result.AppendLine($"Most recent log file: {Path.GetFileName(mostRecentLog)}");
                    result.AppendLine($"Last modified: {new FileInfo(mostRecentLog).LastWriteTime}");
                    result.AppendLine();
                    
                    try
                    {
                        // Read the file with FileShare.ReadWrite to allow concurrent access with Serilog
                        var lines = new List<string>();
                        await using (var stream = new FileStream(mostRecentLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(stream))
                        {
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                lines.Add(line);
                            }
                        }
                        
                        string[] recentLines = lines.TakeLast(count).ToArray();
                        
                        result.AppendLine($"Last {recentLines.Length} log entries:");
                        result.AppendLine(new string('-', 80));
                        
                        foreach (string line in recentLines)
                        {
                            result.AppendLine(line);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"Error reading log file: {ex.Message}");
                    }
                }
                else
                {
                    result.AppendLine("No log files found matching pattern: mcp-code-editor-debug*.log");
                    
                    // List all files in the directory for debugging
                    string[] allFiles = Directory.GetFiles(logDir);
                    if (allFiles.Length > 0)
                    {
                        result.AppendLine("\nFiles in log directory:");
                        foreach (string file in allFiles)
                        {
                            result.AppendLine($"  - {Path.GetFileName(file)}");
                        }
                    }
                }
            }
            else
            {
                result.AppendLine($"Log directory not found: {logDir}");
            }
            
            // Also log current state
            result.AppendLine();
            result.AppendLine("=== Current Application State ===");
            result.AppendLine($"Base Directory: {AppContext.BaseDirectory}");
            result.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
            result.AppendLine($"Process ID: {Environment.ProcessId}");
            result.AppendLine($"Current Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            return result.ToString();
        });
    }
}
